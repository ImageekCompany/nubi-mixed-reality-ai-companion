using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>IApiUsageMeter that records every call and charges a fixed price per settled request.</summary>
    public sealed class RecordingUsageMeter : IApiUsageMeter
    {
        public readonly struct Estimate
        {
            public Estimate(long reservationId, string model, long inputTokens, long outputTokens)
            { ReservationId = reservationId; Model = model; InputTokens = inputTokens; OutputTokens = outputTokens; }
            public long ReservationId { get; }
            public string Model { get; }
            public long InputTokens { get; }
            public long OutputTokens { get; }
        }

        readonly List<decimal> _reservations = new();
        readonly List<UsageRecord> _records = new();
        readonly List<Estimate> _estimates = new();
        readonly HashSet<long> _open = new();
        long _nextId;

        public bool RefuseReservations { get; set; }
        public decimal CostPerRequest { get; set; } = 0.001m;
        public IReadOnlyList<decimal> Reservations => _reservations;
        public IReadOnlyList<UsageRecord> Records => _records;
        public IReadOnlyList<Estimate> Estimates => _estimates;
        /// <summary>Reservations neither recorded nor estimated yet.</summary>
        public int OpenReservations => _open.Count;
        public int Flushes { get; private set; }

        public decimal SpentUsd { get; private set; }
        public decimal HardCapUsd { get; set; } = 10m;
        public bool IsCapReached => SpentUsd >= HardCapUsd;
        public event Action<decimal> SpentChanged;

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            _reservations.Add(estimatedUsd);
            if (RefuseReservations)
            {
                reservationId = 0;
                return false;
            }
            reservationId = ++_nextId;
            _open.Add(reservationId);
            return true;
        }

        public decimal Record(long reservationId, in UsageRecord usage)
        {
            _open.Remove(reservationId);
            _records.Add(usage);
            return Charge();
        }

        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens)
        {
            _open.Remove(reservationId);
            _estimates.Add(new Estimate(reservationId, model, estInputTokens, estOutputTokens));
            return Charge();
        }

        public void Flush() => Flushes++;

        public void Dispose() { }

        decimal Charge()
        {
            SpentUsd += CostPerRequest;
            SpentChanged?.Invoke(SpentUsd);
            return CostPerRequest;
        }
    }
}
