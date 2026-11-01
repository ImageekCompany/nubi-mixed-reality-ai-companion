using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// A token linked to <c>parent</c> that also cancels after <c>timeout</c> on <see cref="IClock"/>, so timeouts are
    /// deterministic under a fake clock and never fire from a thread-pool timer (contracts §0 bans
    /// <c>CancellationTokenSource.CancelAfter</c>). <see cref="TimedOut"/> tells a timeout from a parent cancel.
    /// </summary>
    public sealed class TimeoutScope : IDisposable
    {
        readonly CancellationTokenSource _linked;
        readonly CancellationTokenSource _timer = new();
        bool _disposed;

        public TimeoutScope(IClock clock, TimeSpan timeout, CancellationToken parent)
        {
            _linked = CancellationTokenSource.CreateLinkedTokenSource(parent);
            Watch(clock, timeout).Forget();
        }

        public CancellationToken Token => _linked.Token;
        public bool TimedOut { get; private set; }

        /// <summary>Cancels the scope early (e.g. a superseded call) without counting as a timeout.</summary>
        public void Cancel() => _linked.Cancel();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _timer.Cancel();
            _timer.Dispose();
            _linked.Dispose();
        }

        async UniTaskVoid Watch(IClock clock, TimeSpan timeout)
        {
            var cancelled = await clock.Delay(timeout, _timer.Token).SuppressCancellationThrow();
            if (cancelled || _disposed)
                return;
            TimedOut = true;
            _linked.Cancel();
        }
    }
}
