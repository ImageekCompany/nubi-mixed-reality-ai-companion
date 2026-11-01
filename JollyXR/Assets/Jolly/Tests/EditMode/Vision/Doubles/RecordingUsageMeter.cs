using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    sealed class RecordingUsageMeter : IApiUsageMeter
    {
        long _nextId;

        public bool AllowReservations { get; set; } = true;
        public List<decimal> Reserved { get; } = new();
        public List<(long id, UsageRecord usage)> Recorded { get; } = new();
        public List<(long id, string model)> Estimated { get; } = new();
        public int OpenReservations => Reserved.Count - Recorded.Count - Estimated.Count;

        public decimal SpentUsd => 0m;
        public decimal HardCapUsd => 1m;
        public bool IsCapReached => !AllowReservations;
        public event Action<decimal> SpentChanged { add { } remove { } }

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            reservationId = 0;
            if (!AllowReservations)
                return false;
            Reserved.Add(estimatedUsd);
            reservationId = ++_nextId;
            return true;
        }

        public decimal Record(long reservationId, in UsageRecord usage)
        {
            Recorded.Add((reservationId, usage));
            return 0m;
        }

        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens)
        {
            Estimated.Add((reservationId, model));
            return 0m;
        }

        public void Flush() { }

        public void Dispose() { }
    }
}
