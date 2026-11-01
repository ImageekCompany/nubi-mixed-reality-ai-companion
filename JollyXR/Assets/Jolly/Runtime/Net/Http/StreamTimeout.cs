using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Net.Http
{
    /// <summary>
    /// First-event and inter-event timeout for a stream: <see cref="Token"/> is cancelled when no
    /// <see cref="Restart"/> happens within the current window, or when the parent is cancelled. Timed by
    /// <see cref="IClock.Delay"/> (PlayerLoop on device, FakeClock in tests), never by CancelAfter's thread-pool timer.
    /// </summary>
    public sealed class StreamTimeout : IDisposable
    {
        // Below a millisecond the remaining window is clock noise; waiting on it could spin on zero-length delays.
        const double ResolutionSeconds = 0.001;

        readonly IClock _clock;
        readonly CancellationTokenSource _linked;
        readonly CancellationTokenSource _timer = new();
        double _deadline;
        bool _running;
        bool _disposed;

        public StreamTimeout(IClock clock, CancellationToken parent)
        {
            _clock = clock;
            _linked = CancellationTokenSource.CreateLinkedTokenSource(parent);
        }

        public CancellationToken Token => _linked.Token;
        public bool HasFired { get; private set; }

        public void Restart(TimeSpan window)
        {
            _deadline = _clock.Now + window.TotalSeconds;
            if (_running || _disposed)
                return;
            _running = true;
            RunTimer().Forget();
        }

        async UniTaskVoid RunTimer()
        {
            while (true)
            {
                var remaining = _deadline - _clock.Now;
                if (remaining < ResolutionSeconds)
                {
                    HasFired = true;
                    _linked.Cancel();
                    return;
                }
                if (await _clock.Delay(TimeSpan.FromSeconds(remaining), _timer.Token).SuppressCancellationThrow())
                    return;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _timer.Cancel();
            _timer.Dispose();
            _linked.Dispose();
        }
    }
}
