using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Usage
{
    /// <summary>
    /// Persisted spend meter with reservations (contracts §2). Main thread only. Saves are debounced to 2 s on IClock;
    /// <see cref="Flush"/> and <see cref="Dispose"/> write pending state at once. Settling a reservation that is unknown
    /// or already settled throws instead of charging twice. A failed save is logged and retried by the next flush, so
    /// <see cref="Flush"/> never throws into the lifecycle callbacks that call it.
    /// </summary>
    public sealed class ApiUsageMeter : IApiUsageMeter
    {
        static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(2);

        readonly PriceTable _prices;
        readonly string _filePath;
        readonly IClock _clock;
        readonly ILog _log;
        readonly Dictionary<string, UsageTotals> _models = new();
        readonly Dictionary<long, decimal> _reservations = new();
        readonly CancellationTokenSource _lifetime = new();
        decimal _reserved;
        long _nextReservation;
        bool _dirty;
        bool _saveScheduled;
        bool _disposed;

        public ApiUsageMeter(PriceTable prices, string filePath, decimal hardCapUsd, IClock clock, ILog log)
        {
            _prices = prices;
            _filePath = filePath;
            _clock = clock;
            _log = log;
            HardCapUsd = hardCapUsd;
            UsageFile.TryLoad(filePath, out var spent, _models);
            SpentUsd = spent;
        }

        public decimal SpentUsd { get; private set; }
        public decimal HardCapUsd { get; }
        public bool IsCapReached => SpentUsd >= HardCapUsd;
        public event Action<decimal> SpentChanged;

        /// <summary>Cumulative usage of one model (all zero when unseen), e.g. for the Settings usage view.</summary>
        public UsageTotals TotalsFor(string model) => _models.TryGetValue(model, out var t) ? t : new UsageTotals();

        public bool TryReserve(decimal estimatedUsd, out long reservationId)
        {
            if (estimatedUsd < 0m)
                throw new ArgumentOutOfRangeException(nameof(estimatedUsd), estimatedUsd, "Estimate must not be negative.");
            if (IsCapReached || SpentUsd + _reserved + estimatedUsd > HardCapUsd)
            {
                reservationId = 0;
                return false;
            }
            reservationId = ++_nextReservation;
            _reservations.Add(reservationId, estimatedUsd);
            _reserved += estimatedUsd;
            return true;
        }

        public decimal Record(long reservationId, in UsageRecord usage) => Settle(reservationId, usage, estimated: false);

        public decimal RecordEstimate(long reservationId, string model, long estInputTokens, long estOutputTokens) =>
            Settle(reservationId, new UsageRecord(model, estInputTokens, 0, estOutputTokens), estimated: true);

        public void Flush()
        {
            if (!_dirty)
                return;
            try
            {
                UsageFile.Save(_filePath, SpentUsd, _models);
                _dirty = false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _log.Write(LogLevel.Error, "usage", "evt=usage.save_failed", e);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            Flush();
        }

        decimal Settle(long reservationId, in UsageRecord usage, bool estimated)
        {
            if (!_reservations.Remove(reservationId, out var reserved))
                throw new InvalidOperationException($"Reservation {reservationId} is unknown or already settled.");
            _reserved -= reserved;

            var cost = _prices.Cost(usage);
            var totals = Totals(usage.Model);
            totals.InputTokens += usage.InputTokens;
            totals.CachedInputTokens += usage.CachedInputTokens;
            totals.OutputTokens += usage.OutputTokens;
            totals.Images += usage.Images;
            totals.AudioSeconds += usage.AudioSeconds;
            totals.Requests++;
            if (estimated)
                totals.EstimatedRequests++;
            totals.Usd += cost;
            SpentUsd += cost;

            _dirty = true;
            ScheduleSave();
            SpentChanged?.Invoke(SpentUsd);
            return cost;
        }

        UsageTotals Totals(string model)
        {
            var key = model ?? string.Empty;
            if (!_models.TryGetValue(key, out var totals))
                _models.Add(key, totals = new UsageTotals());
            return totals;
        }

        void ScheduleSave()
        {
            if (_saveScheduled || _disposed)
                return;
            _saveScheduled = true;
            SaveAfterDebounce().Forget();
        }

        async UniTaskVoid SaveAfterDebounce()
        {
            var cancelled = await _clock.Delay(SaveDebounce, _lifetime.Token).SuppressCancellationThrow();
            _saveScheduled = false;
            if (!cancelled)
                Flush();
        }
    }
}
