using UnityEngine;

namespace Jolly.Character
{
    public readonly struct LookAngles
    {
        public LookAngles(float headYaw, float headPitch, float eyeYaw, float eyePitch)
        { HeadYaw = headYaw; HeadPitch = headPitch; EyeYaw = eyeYaw; EyePitch = eyePitch; }

        public float HeadYaw { get; }
        public float HeadPitch { get; }
        public float EyeYaw { get; }
        public float EyePitch { get; }
    }

    /// <summary>
    /// Splits a look direction between the head chain and the eyes. The eye bones pivot deep in the head and sink
    /// into it past ±16° yaw / ±8° pitch, so the head turns first and the eyes add only the last few degrees;
    /// whatever the clamped head and eyes cannot cover is left for the body to turn.
    /// </summary>
    public static class LookAtSolver
    {
        /// <param name="yaw">Target bearing around the body's up axis, degrees, positive to the body's right.</param>
        /// <param name="pitch">Target elevation around the body's right axis, degrees, positive downwards.</param>
        public static LookAngles Solve(float yaw, float pitch, LookSettings s)
        {
            Split(yaw, s.eyeLead, s.headYawMax, s.eyeYawMax, out var headYaw, out var eyeYaw);
            Split(pitch, s.eyeLead, s.headPitchMax, s.eyePitchMax, out var headPitch, out var eyePitch);
            return new LookAngles(headYaw, headPitch, eyeYaw, eyePitch);
        }

        static void Split(float angle, float eyeLead, float headMax, float eyeMax, out float head, out float eye)
        {
            var lead = Mathf.Min(eyeLead, eyeMax);
            lead = Mathf.Clamp(angle, -lead, lead);
            head = Mathf.Clamp(angle - lead, -headMax, headMax);
            eye = Mathf.Clamp(angle - head, -eyeMax, eyeMax);
        }

        /// <summary>Yaw and pitch of <paramref name="direction"/> in a frame given by its forward and up axes.</summary>
        public static void Bearing(Vector3 direction, Vector3 forward, Vector3 up, out float yaw, out float pitch)
        {
            var flat = Vector3.ProjectOnPlane(direction, up);
            if (flat.sqrMagnitude < 1e-8f)
            {
                yaw = 0f;
                pitch = Vector3.Dot(direction, up) > 0f ? -90f : 90f;
                return;
            }
            yaw = Vector3.SignedAngle(forward, flat, up);
            var right = Vector3.Cross(up, flat);
            pitch = Vector3.SignedAngle(flat, direction, right);
        }
    }
}
