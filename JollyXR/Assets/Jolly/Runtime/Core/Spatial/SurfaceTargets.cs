using System;
using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// Picks a piece of furniture from the room scan and the floor spot beside it where Nubi stops, facing it (ADR-0024).
    /// The footprint is the surface's rectangle turned by its yaw; Nubi never targets the top, because the NavMesh never
    /// bakes furniture as walkable and the rig has no climb or sit clips.
    /// </summary>
    public static class SurfaceTargets
    {
        /// <summary>The surface with this label whose centre is nearest to <paramref name="from"/> on the floor plane.</summary>
        public static bool TryNearest(IReadOnlyList<SceneSurface> surfaces, string label, Vector3 from, out SceneSurface surface)
        {
            surface = default;
            var best = float.PositiveInfinity;
            foreach (var candidate in surfaces)
            {
                if (!string.Equals(candidate.Label, label, StringComparison.OrdinalIgnoreCase))
                    continue;
                var distance = FloorDistance(candidate.Pose.position, from);
                if (distance >= best)
                    continue;
                best = distance;
                surface = candidate;
            }
            return !float.IsPositiveInfinity(best);
        }

        /// <summary>The point <paramref name="clearance"/> outside the footprint edge nearest to <paramref name="from"/>,
        /// at from's height (callers snap it onto the NavMesh). From inside the footprint, the nearest edge is used.</summary>
        public static Vector3 Approach(SceneSurface surface, Vector3 from, float clearance)
        {
            var yaw = Yaw(surface.Pose.rotation);
            var center = surface.Pose.position;
            var local = Quaternion.Inverse(yaw) * (from - center);
            var half = new Vector2(surface.Size.x * 0.5f, surface.Size.z * 0.5f);
            var p = new Vector2(local.x, local.z);

            var edge = new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
            Vector2 outward;
            if (edge != p)
            {
                outward = (p - edge).normalized;
            }
            else if (half.x - Mathf.Abs(p.x) < half.y - Mathf.Abs(p.y))
            {
                outward = new Vector2(Sign(p.x), 0f);
                edge = new Vector2(outward.x * half.x, p.y);
            }
            else
            {
                outward = new Vector2(0f, Sign(p.y));
                edge = new Vector2(p.x, outward.y * half.y);
            }

            var spot = edge + outward * clearance;
            var world = center + yaw * new Vector3(spot.x, 0f, spot.y);
            world.y = from.y;
            return world;
        }

        /// <summary>Where Nubi looks: the centre of the surface's top.</summary>
        public static Vector3 LookPoint(SceneSurface surface)
        {
            var center = surface.Pose.position;
            return new Vector3(center.x, surface.Top, center.z);
        }

        static Quaternion Yaw(Quaternion rotation)
        {
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            return forward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);
        }

        static float Sign(float value) => value < 0f ? -1f : 1f;

        static float FloorDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude;
    }
}
