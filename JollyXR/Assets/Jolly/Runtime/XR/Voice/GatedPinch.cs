namespace Jolly.XR.Voice
{
    /// <summary>
    /// One hand's pinch with hysteresis. The gate (no UI hover, palm not toward the head) is checked only when a pinch
    /// starts: a pinch that began gated stays ignored until the fingers open, and a held pinch never drops because the
    /// gate closes later.
    /// </summary>
    public sealed class GatedPinch
    {
        const float CloseBelowMeters = 0.02f;
        const float OpenAboveMeters = 0.035f;

        enum State { Open, Held, Suppressed }

        State _state;

        public bool IsHeld => _state == State.Held;

        /// <param name="pinchDistance">Thumb-to-index tip distance, or null when the hand is not tracked.</param>
        public bool Update(float? pinchDistance, bool gateOpen)
        {
            if (pinchDistance is not { } d || d > OpenAboveMeters)
                _state = State.Open;
            else if (_state == State.Open && d < CloseBelowMeters)
                _state = gateOpen ? State.Held : State.Suppressed;
            return IsHeld;
        }

        public void Reset() => _state = State.Open;
    }
}
