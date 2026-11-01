namespace Jolly.Audio.Capture
{
    /// <summary>
    /// Flags a write head that has not moved for the timeout. Device spike (docs/research/device-spike.md §1a, §2.6):
    /// one cold launch delivered a single buffer and then zeros forever; End + Start is the documented remedy.
    /// </summary>
    sealed class MicrophoneStallWatchdog
    {
        readonly double _timeoutSeconds;
        int _lastPosition;
        double _lastProgress;

        public MicrophoneStallWatchdog(double timeoutSeconds) => _timeoutSeconds = timeoutSeconds;

        public void Reset(double now)
        {
            _lastPosition = 0;
            _lastProgress = now;
        }

        /// <summary>True when the head is still where it was <c>timeout</c> seconds ago.</summary>
        public bool IsStalled(int position, double now)
        {
            if (position != _lastPosition)
            {
                _lastPosition = position;
                _lastProgress = now;
                return false;
            }
            return now - _lastProgress >= _timeoutSeconds;
        }
    }
}
