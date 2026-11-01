using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>
    /// Manual clock. Time moves only through <see cref="Advance"/>, which completes due delays in due order, each
    /// with <see cref="Now"/> set to its due time, so continuations that schedule further delays are honoured within
    /// the same Advance. Single-threaded: drive it from the test thread.
    /// </summary>
    public sealed class FakeClock : IClock
    {
        sealed class PendingDelay
        {
            public TimeSpan Due;
            public long Sequence;
            public CancellationTokenRegistration Registration;
            public readonly UniTaskCompletionSource Source = new();
        }

        readonly List<PendingDelay> _pending = new();
        readonly DateTimeOffset _utcStart;
        TimeSpan _elapsed;
        long _sequence;

        public FakeClock() : this(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)) { }

        public FakeClock(DateTimeOffset utcStart) => _utcStart = utcStart;

        public double Now => _elapsed.TotalSeconds;
        public DateTimeOffset UtcNow => _utcStart + _elapsed;
        public int PendingDelays => _pending.Count;

        public UniTask Delay(TimeSpan delay, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return UniTask.FromCanceled(ct);
            if (delay <= TimeSpan.Zero)
                return UniTask.CompletedTask;

            var pending = new PendingDelay { Due = _elapsed + delay, Sequence = _sequence++ };
            _pending.Add(pending);
            if (ct.CanBeCanceled)
            {
                pending.Registration = ct.Register(() =>
                {
                    if (_pending.Remove(pending))
                        pending.Source.TrySetCanceled(ct);
                });
            }
            return pending.Source.Task;
        }

        public void Advance(TimeSpan by)
        {
            if (by < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(by), by, "Time cannot move backwards.");

            var target = _elapsed + by;
            while (TryTakeNextDue(target, out var next))
            {
                _elapsed = next.Due;
                next.Registration.Dispose();
                next.Source.TrySetResult();
            }
            _elapsed = target;
        }

        bool TryTakeNextDue(TimeSpan target, out PendingDelay next)
        {
            next = null;
            foreach (var p in _pending)
            {
                if (p.Due <= target && (next == null || p.Due < next.Due || (p.Due == next.Due && p.Sequence < next.Sequence)))
                    next = p;
            }
            if (next == null)
                return false;
            _pending.Remove(next);
            return true;
        }
    }
}
