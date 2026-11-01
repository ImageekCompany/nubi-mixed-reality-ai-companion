using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>
    /// Where a box-local mask quad goes so that, seen from the capture camera, it covers exactly its box.
    /// Pixels are top-left origin (contracts §0); the camera looks along +Z with +Y up, so image y flips.
    /// </summary>
    public static class MaskPlacement
    {
        /// <param name="distanceMeters">Distance from the camera to the box centre along its ray.</param>
        /// <returns>World pose of the quad (facing along the camera's +Z) and its size in metres.</returns>
        public static Pose Place(RectInt box, CameraIntrinsics intrinsics, Pose camera, float distanceMeters, out Vector2 size)
        {
            var f = intrinsics.FocalLength;
            var centre = new Vector2(box.x + box.width * 0.5f, box.y + box.height * 0.5f);
            var direction = intrinsics.PixelToCameraDir(centre);
            var depth = distanceMeters / direction.magnitude;

            size = new Vector2(box.width / f.x * depth, box.height / f.y * depth);
            return new Pose(camera.position + camera.rotation * (direction * depth), camera.rotation);
        }
    }
}
