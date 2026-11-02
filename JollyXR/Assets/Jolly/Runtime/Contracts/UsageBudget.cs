using System;

namespace Jolly.Contracts
{
    public readonly struct UsageRecord
    {
        public UsageRecord(string model, long inputTokens, long cachedInputTokens, long outputTokens,
                           int images = 0, double audioSeconds = 0, int webSearches = 0)
        { Model = model; InputTokens = inputTokens; CachedInputTokens = cachedInputTokens;
          OutputTokens = outputTokens; Images = images; AudioSeconds = audioSeconds; WebSearches = webSearches; }

        public string Model { get; }
        public long InputTokens { get; }       // includes cached
        public long CachedInputTokens { get; }
        public long OutputTokens { get; }      // includes reasoning tokens
        public int Images { get; }             // SAM images
        public double AudioSeconds { get; }    // ASR
        public int WebSearches { get; }        // built-in web_search calls of a Responses request (ADR-0048)
    }

    /// <summary>
    /// Persisted, cross-session spend meter for the project's Meta API budget.
    /// Prices come from JollyConfig (per-model table); unknown models are priced at the most expensive known rate.
    /// HardCapUsd is a per-device sub-budget of the project's $140; docs/api-spend.md is the project-wide ledger.
    /// </summary>
    public interface IApiUsageMeter : IDisposable
    {
        decimal SpentUsd { get; }
        decimal HardCapUsd { get; }
        bool IsCapReached { get; }
        /// <summary>Call before every billable request. Reserves estimatedUsd (Spent + Reserved + estimate must fit the cap).
        /// False means refuse: throw ProviderException(BudgetExceeded).</summary>
        bool TryReserve(decimal estimatedUsd, out long reservationId);
        /// <summary>Settles the reservation with actual usage, persists (debounced to 2 s), returns this record's cost.</summary>
        decimal Record(long reservationId, in UsageRecord usage);
        /// <summary>Settles a request aborted before response.completed (barge-in, timeout) with an estimate:
        /// input chars / 4 and the output deltas seen. Input tokens are assumed billed on abort (speculation, conservative).</summary>
        decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens);
        /// <summary>Writes pending state now. Called from OnApplicationPause(true)/OnApplicationFocus(false); Quest does not reliably call OnDestroy.</summary>
        void Flush();
        event Action<decimal> SpentChanged;
    }
}
