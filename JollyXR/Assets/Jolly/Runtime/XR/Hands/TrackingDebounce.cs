namespace Jolly.XR.Hands
{
    /// <summary>
    /// One hand's tracked flag that changes only once the raw flag has held its new value for the hold time. With nobody
    /// wearing the headset the subsystem reported a hand for a moment about once a second (38–56 times per self-test run,
    /// selftest-report.md 6179cfc), and each blip showed and hid the hand mesh. The first sample is taken at once.
    /// </summary>
    public sealed class TrackingDebounce
    {
        /// <summary>ADR-0033: what a real hand adds as lag on appearing and disappearing.</summary>
        public const double DefaultHoldSeconds = 0.2;

        readonly double _holdSeconds;
        bool _started;
        double _changeAt = double.NaN;

        public TrackingDebounce(double holdSeconds = DefaultHoldSeconds) => _holdSeconds = holdSeconds;

        public bool Stable { get; private set; }

        /// <summary>True when <see cref="Stable"/> changed with this sample.</summary>
        public bool Update(bool tracked, double now)
        {
            if (!_started)
            {
                _started = true;
                Stable = tracked;
                return true;
            }
            if (tracked == Stable)
            {
                _changeAt = double.NaN;
                return false;
            }
            if (double.IsNaN(_changeAt))
                _changeAt = now + _holdSeconds;
            if (now < _changeAt)
                return false;
            Stable = tracked;
            _changeAt = double.NaN;
            return true;
        }
    }
}
