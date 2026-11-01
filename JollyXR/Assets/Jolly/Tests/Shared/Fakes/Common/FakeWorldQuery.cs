using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
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
}
