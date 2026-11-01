using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.XR.Navigation;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.XR.World
{
    /// <summary>
    /// IWorldQuery over AR Foundation and the NavGeometry colliders. The depth raycast is the meta-openxr provider's
    /// only native ray type (MetaOpenXRRaycastSubsystem.cs:136,232); plane and box hits come from the colliders
    /// FloorNavMeshBuilder generates. Before USE_SCENE the raycast manager is disabled and no colliders exist.
    /// </summary>
    public sealed class ArWorldQuery : IWorldQuery
    {
        readonly List<ARRaycastHit> _hits = new();
        readonly ARRaycastManager _raycasts;
        readonly ARPlaneManager _planes;
        readonly ARBoundingBoxManager _boxes;
        readonly FloorNavMeshBuilder _floor;
        readonly int _navGeometryMask;

        public ArWorldQuery(ARRaycastManager raycasts, ARPlaneManager planes, ARBoundingBoxManager boxes, FloorNavMeshBuilder floor)
        {
            _raycasts = raycasts;
            _planes = planes;
            _boxes = boxes;
            _floor = floor;
            _navGeometryMask = 1 << floor.NavGeometryLayer;
        }

        public bool HasWalkableArea => _floor.HasNavMesh;

        public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
        {
            var best = float.PositiveInfinity;
            hit = default;
            if (IsRunning(_raycasts) && _raycasts.subsystem is { running: true } && _raycasts.Raycast(ray, _hits, TrackableType.Depth))
            {
                foreach (var h in _hits)
                {
                    if (h.distance <= maxDistance && h.distance < best)
                    {
                        best = h.distance;
                        hit = h.pose.position;
                    }
                }
            }
            if (Physics.Raycast(ray, out var physicsHit, Mathf.Min(maxDistance, best), _navGeometryMask, QueryTriggerInteraction.Ignore))
            {
                best = physicsHit.distance;
                hit = physicsHit.point;
            }
            return !float.IsPositiveInfinity(best);
        }

        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
        {
            return _floor.TrySampleWalkable(near, radius, out point);
        }

        /// <summary>The nearest matching box (or plane) to near, as its centre dropped to the bottom of the box
        /// (or the plane's centre). Callers project it onto the NavMesh to get a reachable spot beside the furniture.</summary>
        public bool TryFindLabeledSurface(string label, Vector3 near, out Vector3 point)
        {
            point = default;
            if (!SurfaceLabels.TryParse(label, out var planeClass, out var boxClass))
                return false;

            var best = float.PositiveInfinity;
            if (boxClass != BoundingBoxClassifications.None && IsRunning(_boxes))
            {
                foreach (var box in _boxes.trackables)
                {
                    if ((box.classifications & boxClass) != 0)
                        Consider(box.transform.position - box.transform.up * (box.size.y * 0.5f), near, ref best, ref point);
                }
            }
            if (planeClass != PlaneClassifications.None && IsRunning(_planes))
            {
                foreach (var plane in _planes.trackables)
                {
                    if ((plane.classifications & planeClass) != 0)
                        Consider(plane.center, near, ref best, ref point);
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        static void Consider(Vector3 candidate, Vector3 near, ref float best, ref Vector3 point)
        {
            var distance = (candidate - near).sqrMagnitude;
            if (distance >= best)
                return;
            best = distance;
            point = candidate;
        }

        static bool IsRunning(Behaviour manager) => manager != null && manager.isActiveAndEnabled;
    }
}
