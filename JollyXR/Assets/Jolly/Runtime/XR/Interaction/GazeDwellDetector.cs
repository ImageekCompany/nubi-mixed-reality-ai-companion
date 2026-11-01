using UnityEngine;

namespace Jolly.XR.Interaction
{
    /// <summary>Head-gaze dwell on Nubi's bounds (architecture §5.4: ≥ 1.5 s). Head gaze, not eye gaze: Quest 3 has no
    /// eye tracking.</summary>
    public sealed class GazeDwellDetector
    {
        const double DwellSeconds = 1.5;
        const float MaxDistanceMeters = 4f;

        readonly DwellTimer _timer = new(DwellSeconds);

        public bool Update(Pose head, Bounds target, double now)
        {
            var gaze = new Ray(head.position, head.rotation * Vector3.forward);
            var onTarget = target.IntersectRay(gaze, out var distance) && distance <= MaxDistanceMeters;
            return _timer.Update(onTarget, now);
        }
    }
}
