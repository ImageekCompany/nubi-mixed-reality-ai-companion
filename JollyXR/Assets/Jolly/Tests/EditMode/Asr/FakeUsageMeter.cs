using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Tests.EditMode.Asr
{
    /// <summary>Records reservations and settlements; prices nothing.</summary>
    sealed class FakeUsageMeter : IApiUsageMeter
    {
        readonly List<UsageRecord> _records = new();
        long _nextReservation = 1;

        public bool RefuseReservations { get; set; }
        public decimal LastReservedUsd { get; private set; }
        public int OpenReservations { get; private set; }
        public IReadOnlyList<UsageRecord> Records => _records;

        public decimal SpentUsd => 0m;
        public decimal HardCapUsd => 1m;
        public bool IsCapReached => RefuseReservations;

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            reservationId = 0;
            if (RefuseReservations)
                return false;
            LastReservedUsd = estimatedUsd;
            OpenReservations++;
            reservationId = _nextReservation++;
            return true;
        }

        public decimal Record(long reservationId, in UsageRecord usage)
        {
            OpenReservations--;
            _records.Add(usage);
            return 0m;
        }

        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens)
        {
            OpenReservations--;
            _records.Add(new UsageRecord(model, estInputTokens, 0, estOutputTokens));
            return 0m;
        }

        public void Flush() { }

        public event Action<decimal> SpentChanged { add { } remove { } }

        public void Dispose() { }
    }
}
