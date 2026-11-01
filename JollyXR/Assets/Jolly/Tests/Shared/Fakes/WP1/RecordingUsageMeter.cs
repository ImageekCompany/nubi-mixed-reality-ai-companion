using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>IApiUsageMeter that records every call and charges a fixed price per settled request. Like the real meter,
    /// it throws when a reservation is settled twice.</summary>
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
        /// <summary>Refuses a reservation while this many are open, as a cap with room for that many does.</summary>
        public int MaxOpenReservations { get; set; } = int.MaxValue;
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
            if (RefuseReservations || _open.Count >= MaxOpenReservations)
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
            Settle(reservationId);
            _records.Add(usage);
            return Charge();
        }

        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens)
        {
            Settle(reservationId);
            _estimates.Add(new Estimate(reservationId, model, estInputTokens, estOutputTokens));
            return Charge();
        }

        public void Flush() => Flushes++;

        public void Dispose() { }

        void Settle(long reservationId)
        {
            if (!_open.Remove(reservationId))
                throw new InvalidOperationException($"Reservation {reservationId} is unknown or already settled.");
        }

        decimal Charge()
        {
            SpentUsd += CostPerRequest;
            SpentChanged?.Invoke(SpentUsd);
            return CostPerRequest;
        }
    }
}
