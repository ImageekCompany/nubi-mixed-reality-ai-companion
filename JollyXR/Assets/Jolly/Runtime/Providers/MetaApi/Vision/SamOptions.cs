using System;
using Jolly.Net.Retry;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>Values from JollyConfig: model id (already resolved by MetaModelResolver), price, timeout and attempts.</summary>
    public sealed class SamOptions
    {
        public SamOptions(string modelId, decimal usdPerImage, TimeSpan timeout, int maxAttempts)
        {
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentException("A SAM model id is required.", nameof(modelId));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
            ModelId = modelId;
            UsdPerImage = usdPerImage;
            Timeout = timeout;
            RetryPolicy = new RetryPolicy(maxAttempts);
        }

        public string ModelId { get; }
        public decimal UsdPerImage { get; }
        /// <summary>
        /// First-event and idle window per attempt. SAM sends every event of a still at once (meta-model-api.md §4.2),
        /// so the first-event window bounds the whole attempt.
        /// </summary>
        public TimeSpan Timeout { get; }
        /// <summary>Applied only until the first event arrives (MetaSseClient).</summary>
        public RetryPolicy RetryPolicy { get; }
    }
}
