using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>
    /// A pinhole camera for tests: +Z forward, +X right, image y grows downwards. Round-trips exactly, which is all the
    /// tools need from WP5's CameraMath.
    /// </summary>
    public sealed class PinholeProjection : Jolly.Core.Tools.ICameraProjection
    {
        public Ray PixelToWorldRay(Vector2 px, CameraIntrinsics k, Pose cam)
        {
            var local = new Vector3((px.x - k.PrincipalPoint.x) / k.FocalLength.x, -(px.y - k.PrincipalPoint.y) / k.FocalLength.y, 1f);
            return new Ray(cam.position, cam.rotation * local.normalized);
        }

        public bool WorldToPixel(Vector3 p, CameraIntrinsics k, Pose cam, out Vector2 px)
        {
            var local = Quaternion.Inverse(cam.rotation) * (p - cam.position);
            px = default;
            if (local.z <= 0)
                return false;
            px = new Vector2(k.PrincipalPoint.x + k.FocalLength.x * local.x / local.z,
                             k.PrincipalPoint.y - k.FocalLength.y * local.y / local.z);
            return true;
        }
    }
}
