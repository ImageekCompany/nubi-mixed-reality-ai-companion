using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Pixel ↔ world projection for the capture camera, with the same shape as WP5's <c>CameraMath</c> (Core/Spatial).
    /// The vision tools depend on this port so they build and test without WP5; the composition root adapts CameraMath.
    /// Pixels are on the encoded image, top-left origin.
    /// </summary>
    public interface ICameraProjection
    {
        Ray PixelToWorldRay(Vector2 px, CameraIntrinsics k, Pose cam);
        bool WorldToPixel(Vector3 p, CameraIntrinsics k, Pose cam, out Vector2 px);
    }
}
