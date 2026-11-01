using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.Sse;

namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>
    /// One open SSE response. Every event restarts the idle window; a silent window or an HTTP failure surfaces as
    /// ProviderException, the caller's cancellation as OperationCanceledException. DisposeAsync aborts the request.
    /// </summary>
    public sealed class MetaSseStream
    {
        readonly IUniTaskAsyncEnumerator<SseEvent> _events;
        readonly IClock _clock;
        readonly double _openedAt;
        readonly StreamTimeout _timeout;
        readonly TimeSpan _idleTimeout;
        readonly CancellationToken _ct;
        TimeSpan _window;
        bool _started;

        internal MetaSseStream(IHttpTransport transport, HttpRequestSpec request, IClock clock, StreamTimeout timeout,
                               TimeSpan firstEventTimeout, TimeSpan idleTimeout, CancellationToken ct)
        {
            _clock = clock;
            _openedAt = clock.Now;
            _timeout = timeout;
            _idleTimeout = idleTimeout;
            _ct = ct;
            _events = transport.StreamSse(request, () => TimeToFirstByte ??= Since(_openedAt)).GetAsyncEnumerator(timeout.Token);
            Arm(firstEventTimeout);
        }

        public SseEvent Current { get; private set; }
        /// <summary>From the request to the first body byte; null until one arrives.</summary>
        public TimeSpan? TimeToFirstByte { get; private set; }
        /// <summary>From the request to the first event; null until one arrives.</summary>
        public TimeSpan? TimeToFirstEvent { get; private set; }

        public async UniTask<bool> MoveNext()
        {
            try
            {
                if (!await _events.MoveNextAsync())
                    return false;
            }
            catch (OperationCanceledException) when (_timeout.HasFired && !_ct.IsCancellationRequested)
            {
                throw _started
                    ? MetaErrors.Timeout("no stream event", _window)
                    : MetaErrors.Timeout("no first stream event", _window, TimeToFirstByte is { } b
                        ? $"first byte at {(int)b.TotalMilliseconds} ms"
                        : "no byte");
            }
            catch (HttpFailureException e)
            {
                throw MetaErrors.FromHttp(e);
            }
            Current = _events.Current;
            TimeToFirstEvent ??= Since(_openedAt);
            _started = true;
            Arm(_idleTimeout);
            return true;
        }

        public async UniTask DisposeAsync()
        {
            try
            {
                await _events.DisposeAsync();
            }
            finally
            {
                _timeout.Dispose();
            }
        }

        TimeSpan Since(double at) => TimeSpan.FromSeconds(_clock.Now - at);

        void Arm(TimeSpan window)
        {
            _window = window;
            _timeout.Restart(window);
        }
    }
}
