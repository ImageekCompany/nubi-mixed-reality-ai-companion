namespace Jolly.Presentation
{
    /// <summary>
    /// Open or closed, and when an open panel closes by itself: <c>idleSeconds</c> after it opened or a control on it was
    /// last used (ADR-0042). Times are <see cref="Jolly.Contracts.IClock"/> seconds.
    /// </summary>
    public sealed class PanelVisibility
    {
        readonly double _idleSeconds;
        double _closeAt = double.PositiveInfinity;

        public PanelVisibility(double idleSeconds) => _idleSeconds = idleSeconds;

        public bool IsOpen { get; private set; }

        /// <summary>True when it was closed; opening an open panel only restarts the idle time.</summary>
        public bool Open(double now)
        {
            var opened = !IsOpen;
            IsOpen = true;
            _closeAt = now + _idleSeconds;
            return opened;
        }

        /// <summary>True when it was open.</summary>
        public bool Close()
        {
            var closed = IsOpen;
            IsOpen = false;
            _closeAt = double.PositiveInfinity;
            return closed;
        }

        public void Touch(double now)
        {
            if (IsOpen)
                _closeAt = now + _idleSeconds;
        }

        public bool IsIdle(double now) => IsOpen && now >= _closeAt;
    }
}
