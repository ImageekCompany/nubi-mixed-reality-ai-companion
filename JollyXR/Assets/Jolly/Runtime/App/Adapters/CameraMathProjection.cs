using Jolly.Contracts;
using Jolly.Core.Spatial;
using Jolly.Core.Tools;
using UnityEngine;

namespace Jolly.App.Adapters
{
    /// <summary>The vision tools' projection port over WP5's <see cref="CameraMath"/>.</summary>
    public sealed class CameraMathProjection : ICameraProjection
    {
        public Ray PixelToWorldRay(Vector2 px, CameraIntrinsics k, Pose cam) => CameraMath.PixelToWorldRay(px, k, cam);

        public bool WorldToPixel(Vector3 p, CameraIntrinsics k, Pose cam, out Vector2 px) =>
            CameraMath.WorldToPixel(p, k, cam, out px);
    }
}
