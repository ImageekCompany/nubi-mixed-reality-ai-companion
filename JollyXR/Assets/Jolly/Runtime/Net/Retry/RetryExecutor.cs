using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Net.Retry
{
    /// <summary>Runs an attempt and retries ProviderExceptions the policy accepts, waiting on IClock between attempts.</summary>
    public sealed class RetryExecutor
    {
        const string Category = "net";

        readonly IClock _clock;
        readonly IRandom _random;
        readonly ILog _log;

        public RetryExecutor(IClock clock, IRandom random, ILog log)
        {
            _clock = clock;
            _random = random;
            _log = log;
        }

        public async UniTask<T> Execute<T>(Func<CancellationToken, UniTask<T>> attempt, RetryPolicy policy, CancellationToken ct)
        {
            for (var made = 1; ; made++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await attempt(ct);
                }
                catch (ProviderException e) when (!ct.IsCancellationRequested && policy.NextDelay(made, e, _random) is { } delay)
                {
                    if (_log.IsEnabled(LogLevel.Warning))
                        _log.Write(LogLevel.Warning, Category, $"evt=net.retry provider={e.Provider} kind={e.Kind} status={e.HttpStatus} " +
                                                              $"attempt={made} delay_ms={(int)delay.TotalMilliseconds} req={e.RequestId}");
                    await _clock.Delay(delay, ct);
                }
            }
        }
    }
}
