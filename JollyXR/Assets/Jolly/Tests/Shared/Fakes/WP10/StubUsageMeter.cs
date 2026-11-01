using System;
using Jolly.Contracts;

namespace Jolly.Testing.Offline
{
    /// <summary>IApiUsageMeter whose cap is flipped by the test; <see cref="ReachCap"/> raises SpentChanged like a real spend.</summary>
    public sealed class StubUsageMeter : IApiUsageMeter
    {
        public decimal SpentUsd { get; private set; }
        public decimal HardCapUsd { get; } = 1m;
        public bool IsCapReached => SpentUsd >= HardCapUsd;
        public bool HasSubscribers => SpentChanged != null;

        public event Action<decimal> SpentChanged;

        public void ReachCap()
        {
            SpentUsd = HardCapUsd;
            SpentChanged?.Invoke(SpentUsd);
        }

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            reservationId = 0;
            return !IsCapReached;
        }

        public decimal Record(long reservationId, in UsageRecord usage) => 0m;
        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens) => 0m;
        public void Flush() { }
        public void Dispose() { }
    }
}
