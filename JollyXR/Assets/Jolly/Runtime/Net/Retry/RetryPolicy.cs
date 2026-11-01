using System;
using Jolly.Contracts;

namespace Jolly.Net.Retry
{
    /// <summary>
    /// Architecture §11: retries 429/500/502/503, network errors and timeouts; never 4xx or 504. Full jitter,
    /// delay = rand(0, min(maxDelay, baseDelay·2^(attempt-1))); Retry-After wins, capped at maxRetryAfter.
    /// </summary>
    public sealed class RetryPolicy
    {
        static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(0.5);
        static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(4);
        static readonly TimeSpan DefaultMaxRetryAfter = TimeSpan.FromSeconds(8);

        readonly TimeSpan _baseDelay;
        readonly TimeSpan _maxDelay;
        readonly TimeSpan _maxRetryAfter;

        public RetryPolicy(int maxAttempts = 3, TimeSpan? baseDelay = null, TimeSpan? maxDelay = null, TimeSpan? maxRetryAfter = null)
        {
            if (maxAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "At least one attempt.");
            MaxAttempts = maxAttempts;
            _baseDelay = baseDelay ?? DefaultBaseDelay;
            _maxDelay = maxDelay ?? DefaultMaxDelay;
            _maxRetryAfter = maxRetryAfter ?? DefaultMaxRetryAfter;
        }

        public int MaxAttempts { get; }

        /// <summary>attempt = attempts made so far (1 after the first failure). Null means give up and rethrow.</summary>
        public TimeSpan? NextDelay(int attempt, ProviderException e, IRandom r)
        {
            if (attempt >= MaxAttempts || !IsRetryable(e))
                return null;
            if (e.RetryAfter is { } retryAfter)
                return retryAfter < _maxRetryAfter ? retryAfter : _maxRetryAfter;

            var cap = Math.Min(_maxDelay.TotalSeconds, _baseDelay.TotalSeconds * Math.Pow(2, attempt - 1));
            return TimeSpan.FromSeconds(r.Range(0f, (float)cap));
        }

        static bool IsRetryable(ProviderException e) =>
            e.IsTransient && e.HttpStatus is 0 or 429 or 500 or 502 or 503;
    }
}
