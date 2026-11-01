using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// Pinhole projection between encoded-image pixels and Unity world space.
    /// Pixels have a top-left origin; the camera pose is a Unity pose whose +Z goes through the image and whose +Y is
    /// image up, so pixel y maps to -Y. No lens distortion: Camera2 reports none for the Quest 3 cameras.
    /// </summary>
    public static class CameraMath
    {
        const float MinDepth = 1e-4f;

        public static Ray PixelToWorldRay(Vector2 px, CameraIntrinsics k, Pose cam) =>
            new(cam.position, cam.rotation * k.PixelToCameraDir(px));

        /// <summary>Projects a world point into the image. False when it is behind the camera or outside the image;
        /// px is still set for points in front of the camera.</summary>
        public static bool WorldToPixel(Vector3 p, CameraIntrinsics k, Pose cam, out Vector2 px)
        {
            var local = Quaternion.Inverse(cam.rotation) * (p - cam.position);
            if (local.z <= MinDepth)
            {
                px = default;
                return false;
            }

            px = new Vector2(
                k.PrincipalPoint.x + k.FocalLength.x * local.x / local.z,
                k.PrincipalPoint.y - k.FocalLength.y * local.y / local.z);
            return px.x >= 0f && px.y >= 0f && px.x < k.Resolution.x && px.y < k.Resolution.y;
        }
    }
}
