using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// A token that cancels when the caller's tokens do or when <see cref="IClock"/> time passes the limit. Driven by
    /// <see cref="IClock.Delay"/> (PlayerLoop in production, manual in tests), never by a thread-pool timer, so a
    /// timeout cannot resume a stream consumer off the main thread (contracts §0).
    /// </summary>
    sealed class ClockDeadline : IDisposable
    {
        readonly CancellationTokenSource _cts;
        readonly CancellationToken _outerA;
        readonly CancellationToken _outerB;
        bool _expired;

        public ClockDeadline(IClock clock, TimeSpan limit, CancellationToken outerA, CancellationToken outerB = default)
        {
            _outerA = outerA;
            _outerB = outerB;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(outerA, outerB);
            ExpireAfter(clock, limit).Forget();
        }

        public CancellationToken Token => _cts.Token;

        /// <summary>True when the limit, not a caller, caused the cancellation.</summary>
        public bool IsExpired => _expired && !_outerA.IsCancellationRequested && !_outerB.IsCancellationRequested;

        async UniTaskVoid ExpireAfter(IClock clock, TimeSpan limit)
        {
            if (await clock.Delay(limit, _cts.Token).SuppressCancellationThrow())
                return;
            _expired = true;
            _cts.Cancel();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
