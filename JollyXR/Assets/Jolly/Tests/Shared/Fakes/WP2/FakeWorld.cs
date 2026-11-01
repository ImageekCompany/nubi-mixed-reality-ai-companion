using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>IUserPoseProvider with settable head, pointing ray and hand key points.</summary>
    public sealed class FakeUserPose : IUserPoseProvider
    {
        public Pose Head { get; set; } = new(new Vector3(0, 1.6f, 0), Quaternion.identity);
        public Ray? PointingRay { get; set; }
        public List<Vector3> HandKeyPoints { get; } = new();

        public bool TryGetPointingRay(out Ray ray)
        {
            ray = PointingRay ?? default;
            return PointingRay.HasValue;
        }

        public void GetHandKeyPoints(List<Vector3> into)
        {
            into.Clear();
            into.AddRange(HandKeyPoints);
        }
    }

    /// <summary>
    /// IWorldQuery with one infinite vertical wall at <see cref="WallZ"/> (null = nothing to hit), walkable floor
    /// everywhere unless <see cref="Walkable"/> is false, and named surfaces.
    /// </summary>
    public sealed class FakeWorldQuery : IWorldQuery
    {
        public float? WallZ { get; set; }
        public bool Walkable { get; set; } = true;
        public Dictionary<string, Vector3> Surfaces { get; } = new();
        public string LastSurfaceQuery { get; private set; }

        public bool HasWalkableArea => Walkable;

        public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
        {
            hit = default;
            if (WallZ is not { } z || Mathf.Abs(ray.direction.z) < 1e-6f)
                return false;
            var t = (z - ray.origin.z) / ray.direction.z;
            if (t < 0 || t > maxDistance)
                return false;
            hit = ray.GetPoint(t);
            return true;
        }

        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
        {
            point = new Vector3(near.x, 0, near.z);
            return Walkable;
        }

        public bool TryFindLabeledSurface(string label, Vector3 near, out Vector3 point)
        {
            LastSurfaceQuery = label;
            return Surfaces.TryGetValue(label, out point);
        }
    }

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
