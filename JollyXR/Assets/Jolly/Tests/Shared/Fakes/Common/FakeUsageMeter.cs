using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>
    /// IApiUsageMeter for spend displays and cap gates: <see cref="Spend"/> and <see cref="ReachCap"/> raise SpentChanged
    /// like a real spend, reservations succeed until the cap is reached, and settling is free. Tests that check how a
    /// provider reserves and settles use <see cref="RecordingUsageMeter"/>.
    /// </summary>
    public sealed class FakeUsageMeter : IApiUsageMeter
    {
        public FakeUsageMeter(decimal spentUsd = 0m, decimal hardCapUsd = 1m)
        {
            SpentUsd = spentUsd;
            HardCapUsd = hardCapUsd;
        }

        public decimal SpentUsd { get; private set; }
        public decimal HardCapUsd { get; }
        public bool IsCapReached => SpentUsd >= HardCapUsd;
        public bool HasSubscribers => SpentChanged != null;
        public event Action<decimal> SpentChanged;

        public void Spend(decimal usd)
        {
            SpentUsd += usd;
            SpentChanged?.Invoke(SpentUsd);
        }

        public void ReachCap() => Spend(HardCapUsd - SpentUsd);

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            reservationId = 1;
            return !IsCapReached;
        }

        public decimal Record(long reservationId, in UsageRecord usage) => 0m;
        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens) => 0m;
        public void Flush() { }
        public void Dispose() { }
    }
}
