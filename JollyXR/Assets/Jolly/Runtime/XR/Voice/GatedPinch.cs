namespace Jolly.XR.Voice
{
    /// <summary>
    /// One hand's pinch with hysteresis. The gate (no UI hover, palm not toward the head) is checked only when a pinch
    /// starts: a pinch that began gated stays ignored until the fingers open, and a held pinch never drops because the
    /// gate closes later. A held pinch rides out tracking dropouts up to DropoutGraceSeconds (a low or out-of-view hand
    /// loses tracking for a few frames), so a blink in tracking does not cut the utterance short.
    /// </summary>
    public sealed class GatedPinch
    {
        public const double DropoutGraceSeconds = 0.2;
        const float CloseBelowMeters = 0.02f;
        const float OpenAboveMeters = 0.035f;

        enum State { Open, Held, Suppressed }

        State _state;
        double _lostAt = double.NaN;

        public bool IsHeld => _state == State.Held;

        /// <param name="pinchDistance">Thumb-to-index tip distance, or null when the hand is not tracked.</param>
        /// <param name="now">Monotonic seconds (IClock.Now).</param>
        public bool Update(float? pinchDistance, bool gateOpen, double now)
        {
            if (pinchDistance is not { } d)
            {
                if (_state != State.Held)
                    _state = State.Open;
                else if (double.IsNaN(_lostAt))
                    _lostAt = now;
                else if (now - _lostAt > DropoutGraceSeconds)
                    Reset();
                return IsHeld;
            }

            _lostAt = double.NaN;
            if (d > OpenAboveMeters)
                _state = State.Open;
            else if (_state == State.Open && d < CloseBelowMeters)
                _state = gateOpen ? State.Held : State.Suppressed;
            return IsHeld;
        }

        public void Reset()
        {
            _state = State.Open;
            _lostAt = double.NaN;
        }
    }
}
