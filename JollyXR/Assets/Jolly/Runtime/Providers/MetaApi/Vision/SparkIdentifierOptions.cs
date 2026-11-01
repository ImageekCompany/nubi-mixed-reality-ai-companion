using System;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>Values from JollyConfig. The model itself is chosen by the injected <see cref="Jolly.Contracts.IChatModel"/>.</summary>
    public sealed class SparkIdentifierOptions
    {
        public SparkIdentifierOptions(TimeSpan timeout, int maxOutputTokens, string safetyIdentifier)
        {
            if (string.IsNullOrEmpty(safetyIdentifier))
                throw new ArgumentException("A per-install safety identifier is required.", nameof(safetyIdentifier));
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
            if (maxOutputTokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxOutputTokens), maxOutputTokens, "Must be positive.");
            Timeout = timeout;
            MaxOutputTokens = maxOutputTokens;
            SafetyIdentifier = safetyIdentifier;
        }

        /// <summary>Total budget for the identification stream (8 s by default config, architecture §11).</summary>
        public TimeSpan Timeout { get; }
        /// <summary>Includes reasoning tokens, so it must not be capped tightly.</summary>
        public int MaxOutputTokens { get; }
        /// <summary>Hash of the per-install id (architecture "Security and privacy"); never a hardware id.</summary>
        public string SafetyIdentifier { get; }
    }
}
