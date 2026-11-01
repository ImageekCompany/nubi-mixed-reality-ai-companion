using Jolly.Contracts;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// The move target under the user's ray, refreshed by <see cref="Tick"/> every frame, and the floor ring that shows
    /// it (ADR-0044). A pinch captures the target at its start, so the release, which jolts the ray, cannot move it; a
    /// talk hold hides the ring until it ends. Main thread only.
    /// </summary>
    public sealed class PointerTargeting
    {
        readonly IPointerInput _pointer;
        readonly MoveTargetResolver _resolver;
        readonly IMoveMarker _marker;
        MoveTarget _target;
        bool _hasTarget;
        bool _captured;
        bool _suspended;

        public PointerTargeting(IPointerInput pointer, MoveTargetResolver resolver, IMoveMarker marker)
        {
            _pointer = pointer;
            _resolver = resolver;
            _marker = marker;
        }

        public bool IsCaptured => _captured;

        public void Tick()
        {
            if (_captured)
                return;
            Refresh();
        }

        /// <summary>At a pinch: resolves the target under the pinching hand's ray now and holds it until
        /// <see cref="TryTake"/> or <see cref="Suspend"/>. False when there is none.</summary>
        public bool TryCapture()
        {
            if (_suspended)
                return false;
            Refresh();
            _captured = _hasTarget;
            return _captured;
        }

        /// <summary>The captured target, released for the next pinch.</summary>
        public bool TryTake(out MoveTarget target)
        {
            target = _target;
            var taken = _captured && _hasTarget;
            _captured = false;
            return taken;
        }

        /// <summary>A talk hold: no ring and no capture until <see cref="Resume"/>.</summary>
        public void Suspend()
        {
            _suspended = true;
            _captured = false;
            _hasTarget = false;
            _marker.Hide();
        }

        public void Resume() => _suspended = false;

        void Refresh()
        {
            _hasTarget = !_suspended && _pointer.TryGetAim(out var ray, out _) && _resolver.TryResolve(ray, out _target);
            if (_hasTarget)
                _marker.Show(_target.Point);
            else
                _marker.Hide();
        }
    }
}
