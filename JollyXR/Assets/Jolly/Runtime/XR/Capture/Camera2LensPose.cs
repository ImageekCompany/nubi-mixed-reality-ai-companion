using UnityEngine;

namespace Jolly.XR.Capture
{
    /// <summary>
    /// Converts Android Camera2 LENS_POSE_TRANSLATION / LENS_POSE_ROTATION into a Unity pose relative to the pose
    /// reference (the gyroscope on Quest 3, device-spike.md §2.1).
    /// Derived from the Camera2 reference: the sensor frame is right-handed (x right, y up, z toward the viewer) and
    /// a sensor-frame point maps to camera coordinates (x right, y image-down, z optical axis) as R·(p − t).
    /// The result follows CameraFrame.CameraPoseWorld: +Z through the image, +Y image up.
    /// </summary>
    public static class Camera2LensPose
    {
        public static Pose ToUnity(Vector3 translation, Quaternion rotation)
        {
            var cameraToSensor = Quaternion.Inverse(Normalised(rotation));
            var forward = FlipZ(cameraToSensor * Vector3.forward);
            var imageDown = FlipZ(cameraToSensor * Vector3.up);
            return new Pose(FlipZ(translation), Quaternion.LookRotation(forward, -imageDown));
        }

        static Vector3 FlipZ(Vector3 v) => new(v.x, v.y, -v.z);

        static Quaternion Normalised(Quaternion q)
        {
            var n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return n > 0f ? new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n) : Quaternion.identity;
        }
    }
}
