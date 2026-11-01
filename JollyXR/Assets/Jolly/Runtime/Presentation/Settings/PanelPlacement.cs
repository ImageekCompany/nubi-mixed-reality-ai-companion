using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>Where an on-demand panel opens: straight ahead of the face at a reading distance, level, facing back.</summary>
    public static class PanelPlacement
    {
        /// <param name="distance">Horizontal metres from the eyes.</param>
        /// <param name="drop">Metres below eye height, toward the chest.</param>
        public static Pose InFrontOf(Vector3 head, Vector3 headForward, float distance, float drop)
        {
            var forward = Vector3.ProjectOnPlane(headForward, Vector3.up);
            // Looking straight up or down leaves no heading; any horizontal one will do.
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            // A world-space canvas reads correctly with its forward pointing away from the viewer.
            return new Pose(head + forward * distance + Vector3.down * drop, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
