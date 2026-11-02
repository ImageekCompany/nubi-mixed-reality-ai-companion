using System;
using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>
    /// IWorldQuery with one infinite vertical wall at <see cref="WallZ"/> (null = nothing to hit), walkable floor
    /// everywhere unless <see cref="Walkable"/> is false, and the room scan's labelled surfaces.
    /// </summary>
    public sealed class FakeWorldQuery : IWorldQuery
    {
        public float? WallZ { get; set; }
        public bool Walkable { get; set; } = true;
        /// <summary>Reported while <see cref="Walkable"/> is true.</summary>
        public WalkableArea Area { get; set; } = WalkableArea.Scanned;
        public List<SceneSurface> Surfaces { get; } = new();
        public Vector3? LastSampleNear { get; private set; }
        /// <summary>Where a sample near a point lands, e.g. pulled towards an obstacle; the point itself by default.</summary>
        public Func<Vector3, Vector3> Snap { get; set; }

        public WalkableArea WalkableArea => Walkable ? Area : WalkableArea.None;

        /// <summary>A box of the given size standing on the floor (y = 0) with no rotation.</summary>
        public SceneSurface AddBox(string label, Vector3 floorCenter, Vector3 size)
        {
            var surface = new SceneSurface(label,
                new Pose(floorCenter + Vector3.up * (size.y * 0.5f), Quaternion.identity), size);
            Surfaces.Add(surface);
            return surface;
        }

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
            LastSampleNear = near;
            var snapped = Snap?.Invoke(near) ?? near;
            point = new Vector3(snapped.x, 0, snapped.z);
            return Walkable;
        }

        public void GetSurfaces(List<SceneSurface> into)
        {
            into.Clear();
            into.AddRange(Surfaces);
        }
    }
}
