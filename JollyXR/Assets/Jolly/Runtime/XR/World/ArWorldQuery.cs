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

        public WalkableArea WalkableArea => _floor.WalkableArea;

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

        /// <summary>Every classified bounding box (pose = its centre) and every upward horizontal plane classified as a
        /// table top, couch or seat (pose = the plane's centre, its rectangle in the plane's x/z).</summary>
        public void GetSurfaces(List<SceneSurface> into)
        {
            into.Clear();
            if (IsRunning(_boxes))
            {
                foreach (var box in _boxes.trackables)
                {
                    var label = SurfaceLabels.ForBox(box.classifications);
                    if (label != null)
                        into.Add(new SceneSurface(label, new Pose(box.transform.position, box.transform.rotation), box.size));
                }
            }
            if (IsRunning(_planes))
            {
                foreach (var plane in _planes.trackables)
                {
                    var label = plane.alignment == PlaneAlignment.HorizontalUp ? SurfaceLabels.ForPlane(plane.classifications) : null;
                    if (label != null)
                        into.Add(new SceneSurface(label, new Pose(plane.center, plane.transform.rotation),
                            new Vector3(plane.size.x, 0f, plane.size.y)));
                }
            }
        }

        static bool IsRunning(Behaviour manager) => manager != null && manager.isActiveAndEnabled;
    }
}
