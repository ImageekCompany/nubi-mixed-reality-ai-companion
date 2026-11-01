using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Cancels a token source after a delay on the injected <see cref="IClock"/>, so the cancel runs on the clock's
    /// (main) thread, never on a thread-pool timer (contracts §0), and tests drive it with FakeClock. Dispose before the
    /// target source.
    /// </summary>
    sealed class ClockTimeout : IDisposable
    {
        readonly CancellationTokenSource _stop = new();

        public ClockTimeout(IClock clock, TimeSpan after, CancellationTokenSource target) =>
            RunAsync(clock, after, target).Forget();

        public bool Fired { get; private set; }

        public void Dispose()
        {
            _stop.Cancel();
            _stop.Dispose();
        }

        async UniTaskVoid RunAsync(IClock clock, TimeSpan after, CancellationTokenSource target)
        {
            if (await clock.Delay(after, _stop.Token).SuppressCancellationThrow())
                return;
            Fired = true;
            target.Cancel();
        }
    }
}
