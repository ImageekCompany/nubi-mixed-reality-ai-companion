using System;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>Values from JollyConfig: model id (already resolved by MetaModelResolver), price and total timeout.</summary>
    public sealed class SamOptions
    {
        public SamOptions(string modelId, decimal usdPerImage, TimeSpan timeout)
        {
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentException("A SAM model id is required.", nameof(modelId));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
            ModelId = modelId;
            UsdPerImage = usdPerImage;
            Timeout = timeout;
        }

        public string ModelId { get; }
        public decimal UsdPerImage { get; }
        public TimeSpan Timeout { get; }
    }
}
