using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>A flat floor at y = 0 plus optional labelled surfaces. Raycasts hit the floor or a fixed distance.</summary>
    public sealed class FakeWorldQuery : IWorldQuery
    {
        public bool HasWalkableArea { get; set; } = true;
        /// <summary>When set, every raycast hits at this distance along the ray.</summary>
        public float? RaycastDistance { get; set; }
        public Dictionary<string, Vector3> LabeledSurfaces { get; } = new();

        public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
        {
            if (RaycastDistance is { } d && d <= maxDistance)
            {
                hit = ray.GetPoint(d);
                return true;
            }
            var floor = new Plane(Vector3.up, Vector3.zero);
            if (floor.Raycast(ray, out var enter) && enter <= maxDistance)
            {
                hit = ray.GetPoint(enter);
                return true;
            }
            hit = default;
            return false;
        }

        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
        {
            point = new Vector3(near.x, 0f, near.z);
            return HasWalkableArea && Mathf.Abs(near.y) <= radius + 2f;
        }

        public bool TryFindLabeledSurface(string label, Vector3 near, out Vector3 point) =>
            LabeledSurfaces.TryGetValue(label.ToLowerInvariant(), out point);
    }
}
