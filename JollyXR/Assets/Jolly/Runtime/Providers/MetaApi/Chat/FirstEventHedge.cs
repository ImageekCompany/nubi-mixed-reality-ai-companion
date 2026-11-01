using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Common;

namespace Jolly.Providers.MetaApi.Chat
{
    public enum HedgeLeg { Original, Hedge }

    /// <summary>
    /// Opens a Responses stream with a hedge (ADR-0038): when the original request has no first event after
    /// <c>hedgeAfter</c>, or fails with a retryable error before then, an identical second request is sent if the
    /// budget allows it. The first leg to open wins and the other is aborted. Both legs share the original's
    /// first-event deadline, so a hedge never makes the caller wait longer than an unhedged request would.
    /// Every leg that does not win is passed to <c>abandoned</c> exactly once, after its request has ended.
    /// Timed by <see cref="IClock"/>. Main thread only.
    /// </summary>
    public sealed class FirstEventHedge
    {
        public delegate UniTask<MetaSseStream> OpenLeg(HedgeLeg leg, TimeSpan firstEventTimeout, CancellationToken ct);

        const string Category = "llm";

        readonly IClock _clock;
        readonly ILog _log;

        public FirstEventHedge(IClock clock, ILog log)
        {
            _clock = clock;
            _log = log;
        }

        /// <summary>
        /// Completes with the winning leg once its first event has arrived. A hedgeAfter of zero, or not shorter than
        /// the first-event timeout, opens the original alone. Throws the last leg's failure when no leg opens.
        /// </summary>
        public UniTask<(HedgeLeg Leg, MetaSseStream Stream)> Open(OpenLeg open, Func<bool> tryAddHedge, Action<HedgeLeg> abandoned,
                                                                 TimeSpan hedgeAfter, TimeSpan firstEventTimeout, CancellationToken ct)
        {
            var race = new Race(this, open, tryAddHedge, abandoned, firstEventTimeout, ct);
            race.Start(hedgeAfter > TimeSpan.Zero && hedgeAfter < firstEventTimeout ? hedgeAfter : (TimeSpan?)null);
            return race.Result;
        }

        sealed class Race
        {
            readonly FirstEventHedge _owner;
            readonly OpenLeg _open;
            readonly Func<bool> _tryAddHedge;
            readonly Action<HedgeLeg> _abandoned;
            readonly TimeSpan _firstEventTimeout;
            readonly CancellationToken _ct;
            readonly CancellationTokenSource[] _legs = new CancellationTokenSource[2];
            readonly string[] _failures = new string[2];
            readonly UniTaskCompletionSource<(HedgeLeg, MetaSseStream)> _result = new();
            CancellationTokenSource _timer;
            double _sentAt;
            int _running;
            bool _hedged;
            bool _decided;
            bool _allTimedOut = true;
            Exception _lastFailure;

            public Race(FirstEventHedge owner, OpenLeg open, Func<bool> tryAddHedge, Action<HedgeLeg> abandoned,
                        TimeSpan firstEventTimeout, CancellationToken ct)
            {
                _owner = owner;
                _open = open;
                _tryAddHedge = tryAddHedge;
                _abandoned = abandoned;
                _firstEventTimeout = firstEventTimeout;
                _ct = ct;
            }

            public UniTask<(HedgeLeg, MetaSseStream)> Result => _result.Task;

            public void Start(TimeSpan? hedgeAfter)
            {
                _sentAt = _owner._clock.Now;
                if (hedgeAfter is { } after)
                {
                    _timer = CancellationTokenSource.CreateLinkedTokenSource(_ct);
                    HedgeWhenSilent(after, _timer.Token).Forget();
                }
                Run(HedgeLeg.Original, _firstEventTimeout).Forget();
            }

            async UniTaskVoid HedgeWhenSilent(TimeSpan after, CancellationToken timer)
            {
                if (await _owner._clock.Delay(after, timer).SuppressCancellationThrow())
                    return;
                TryHedge("silent");
            }

            async UniTaskVoid Run(HedgeLeg leg, TimeSpan firstEventTimeout)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
                _legs[(int)leg] = cts;
                _running++;
                MetaSseStream stream;
                try
                {
                    stream = await _open(leg, firstEventTimeout, cts.Token);
                }
                catch (Exception e)
                {
                    End(leg);
                    cts.Dispose();
                    _abandoned(leg);
                    OnFailed(leg, e);
                    return;
                }
                End(leg);
                if (!_decided)
                {
                    // Not disposed: the winner's stream keeps reading with this token, and disposing a linked source
                    // unlinks it from the caller's cancellation. It holds no timer; the caller's source releases it.
                    Win(leg, stream);
                    return;
                }
                try
                {
                    await stream.DisposeAsync();
                }
                finally
                {
                    cts.Dispose();
                    _abandoned(leg);
                }
            }

            void End(HedgeLeg leg)
            {
                _legs[(int)leg] = null;
                _running--;
            }

            void Win(HedgeLeg leg, MetaSseStream stream)
            {
                _decided = true;
                StopTimer();
                _legs[(int)Other(leg)]?.Cancel();
                if (_hedged)
                    Log(leg == HedgeLeg.Original ? "won_original" : "won_hedge", null);
                _owner.LogFirstEvent(leg, stream);
                _result.TrySetResult((leg, stream));
            }

            void OnFailed(HedgeLeg leg, Exception e)
            {
                _lastFailure = e;
                _failures[(int)leg] = Describe(e);
                if (e is not ProviderException { Kind: ProviderErrorKind.Timeout })
                    _allTimedOut = false;
                if (_decided)
                    return;
                // A fast 5xx or connection error is the retry's case: the hedge replaces the sequential retry.
                if (leg == HedgeLeg.Original && _timer != null && !_ct.IsCancellationRequested &&
                    e is ProviderException p && RetryPolicy.IsRetryable(p) && TryHedge("failed"))
                    return;
                if (_running > 0)
                    return;
                _decided = true;
                StopTimer();
                if (_hedged && !_ct.IsCancellationRequested)
                    Log(_allTimedOut ? "both_timeout" : "both_failed",
                        $"original=\"{_failures[(int)HedgeLeg.Original]}\" hedge=\"{_failures[(int)HedgeLeg.Hedge]}\"");
                _result.TrySetException(_lastFailure);
            }

            bool TryHedge(string reason)
            {
                if (_decided || _hedged)
                    return false;
                StopTimer();
                var elapsed = _owner._clock.Now - _sentAt;
                var remaining = _firstEventTimeout - TimeSpan.FromSeconds(elapsed);
                if (remaining <= TimeSpan.Zero)
                    return false;
                if (!_tryAddHedge())
                {
                    Log("skipped", "reason=budget");
                    return false;
                }
                _hedged = true;
                Log("started", "reason=" + reason);
                Run(HedgeLeg.Hedge, remaining).Forget();
                return true;
            }

            void StopTimer()
            {
                if (_timer == null)
                    return;
                _timer.Cancel();
                _timer.Dispose();
                _timer = null;
            }

            void Log(string status, string extra)
            {
                var log = _owner._log;
                if (!log.IsEnabled(LogLevel.Info))
                    return;
                var elapsedMs = (int)((_owner._clock.Now - _sentAt) * 1000);
                log.Write(LogLevel.Info, Category, $"evt=llm.hedge status={status} elapsed_ms={elapsedMs}" +
                                                   (extra == null ? "" : " " + extra));
            }

            /// <summary>A timeout's own text says whether any byte arrived; other failures may quote the body, so only the kind.</summary>
            static string Describe(Exception e) => e switch
            {
                ProviderException { Kind: ProviderErrorKind.Timeout } p => p.Message,
                ProviderException p => p.Kind.ToString(),
                _ => e.GetType().Name,
            };

            static HedgeLeg Other(HedgeLeg leg) => leg == HedgeLeg.Original ? HedgeLeg.Hedge : HedgeLeg.Original;
        }

        /// <summary>
        /// First byte vs first event of the winner: a late first byte is the connection or the server's queue, a late
        /// event after an early byte is the model.
        /// </summary>
        void LogFirstEvent(HedgeLeg leg, MetaSseStream stream)
        {
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, Category, $"evt=llm.first_event leg={(leg == HedgeLeg.Original ? "original" : "hedge")} " +
                                                    $"ttfb_ms={Ms(stream.TimeToFirstByte)} ttfe_ms={Ms(stream.TimeToFirstEvent)}");
        }

        static string Ms(TimeSpan? t) =>
            t is { } v ? ((int)v.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) : "none";
    }
}
