using System;
using Jolly.Contracts;

namespace Jolly.Core.Offline
{
    /// <summary>
    /// Turns raw interaction signals into canned-line requests, at most one per <see cref="DefaultMinInterval"/> across
    /// all kinds (architecture §5.4). A signal while canSpeak() is false (a turn or a line in progress) is dropped and
    /// does not use up the interval. Plays nothing itself: subscribers route LineRequested to IOfflineResponder.
    /// </summary>
    public sealed class InteractionTriggers : IDisposable
    {
        public static readonly TimeSpan DefaultMinInterval = TimeSpan.FromSeconds(20);

        readonly IInteractionSignals _signals;
        readonly IClock _clock;
        readonly Func<bool> _canSpeak;
        readonly double _minIntervalSeconds;
        double _lastRequestAt = double.NegativeInfinity;
        bool _disposed;

        public InteractionTriggers(IInteractionSignals signals, IClock clock, Func<bool> canSpeak, TimeSpan? minInterval = null)
        {
            _signals = signals;
            _clock = clock;
            _canSpeak = canSpeak;
            _minIntervalSeconds = (minInterval ?? DefaultMinInterval).TotalSeconds;
            _signals.GazeDwell += OnGreetingSignal;
            _signals.OpenPalm += OnGreetingSignal;
            _signals.Poke += OnPoke;
        }

        /// <summary>Raised on the main thread with the category to speak.</summary>
        public event Action<LineCategory> LineRequested;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _signals.GazeDwell -= OnGreetingSignal;
            _signals.OpenPalm -= OnGreetingSignal;
            _signals.Poke -= OnPoke;
        }

        void OnGreetingSignal() => Request(LineCategory.Greeting);

        void OnPoke() => Request(LineCategory.WhoAmI);

        void Request(LineCategory category)
        {
            var now = _clock.Now;
            if (now - _lastRequestAt < _minIntervalSeconds || !_canSpeak())
                return;
            _lastRequestAt = now;
            LineRequested?.Invoke(category);
        }
    }
}
