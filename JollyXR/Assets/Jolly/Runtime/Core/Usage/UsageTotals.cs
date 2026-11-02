namespace Jolly.Core.Usage
{
    /// <summary>Cumulative usage of one model; also the persistence DTO (public fields, mapped by hand, ADR-0015).</summary>
    public sealed class UsageTotals
    {
        public long InputTokens;
        public long CachedInputTokens;
        public long OutputTokens;
        public long Images;
        public double AudioSeconds;
        public long WebSearches;
        public long Requests;
        /// <summary>Requests settled with an estimate because they ended before response.completed.</summary>
        public long EstimatedRequests;
        public decimal Usd;
    }
}
