using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    public sealed class FakeUsageMeter : IApiUsageMeter
    {
        public decimal SpentUsd { get; private set; } = 0.53m;
        public decimal HardCapUsd => 5m;
        public bool IsCapReached => false;
        public event Action<decimal> SpentChanged;

        public void Spend(decimal usd)
        {
            SpentUsd += usd;
            SpentChanged?.Invoke(SpentUsd);
        }

        public bool TryReserve(decimal estimatedUsd, out long reservationId) { reservationId = 0; return true; }
        public decimal Record(long reservationId, in UsageRecord usage) => 0m;
        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens) => 0m;
        public void Flush() { }
        public void Dispose() { }
    }
}
