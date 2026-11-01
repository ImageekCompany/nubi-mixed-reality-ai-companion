using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.XR.Navigation
{
    /// <summary>
    /// Turns Space Setup trackables into NavMesh geometry: Floor planes become mesh colliders (their real, possibly
    /// concave outline) and every bounding box a box collider baked as Not Walkable. The colliders live on the
    /// NavGeometry layer, so IWorldQuery can also raycast them. Rebuilds are throttled by NavMeshBaker. Without Space
    /// Setup, the room-scan flow may add a fallback floor polygon around the user instead (ADR-0024).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] ARPlaneManager planes;
        [SerializeField] ARBoundingBoxManager boxes;
        [Tooltip("Layer for the generated floor and furniture colliders (NavGeometry).")]
        [SerializeField] int navGeometryLayer = 8;

        readonly Dictionary<TrackableId, SceneCollider> _colliders = new();
        readonly HashSet<TrackableId> _floorPlanes = new();
        readonly List<Vector2> _outline = new();
        readonly List<Vector3> _vertices = new();
        readonly List<int> _triangles = new();
        NavMeshBaker _baker;
        IClock _clock;
        GameObject _fallback;
        Mesh _fallbackMesh;

        public int NavGeometryLayer => navGeometryLayer;
        public bool HasNavMesh => _baker != null && _baker.HasNavMesh;
        public bool HasScannedFloor => _floorPlanes.Count > 0;
        public bool HasFallbackFloor => _fallback != null;

        public WalkableArea WalkableArea =>
            !HasNavMesh ? WalkableArea.None
            : HasScannedFloor ? WalkableArea.Scanned
            : HasFallbackFloor ? WalkableArea.Fallback
            : WalkableArea.None;

        public void Construct(IClock clock) => _clock = clock;

        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
        {
            point = default;
            return _baker != null && _baker.TrySampleWalkable(near, radius, out point);
        }

        /// <summary>Bakes the outline (x, y → world x, z offsets around center) at floorHeight as walkable ground, moving
        /// the previous fallback floor if there is one. False if the outline does not triangulate.</summary>
        public bool SetFallbackFloor(Vector3 center, IReadOnlyList<Vector2> outline, float floorHeight)
        {
            if (_fallback == null)
            {
                _fallback = new GameObject("FallbackFloor") { layer = navGeometryLayer };
                _fallbackMesh = new Mesh { name = "FallbackFloor" };
            }
            if (!FloorMesh.TryBuild(outline, _fallbackMesh, _vertices, _triangles))
            {
                ClearFallbackFloor();
                return false;
            }
            _fallback.transform.SetPositionAndRotation(new Vector3(center.x, floorHeight, center.z), Quaternion.identity);
            var collider = _fallback.TryGetComponent<MeshCollider>(out var existing) ? existing : _fallback.AddComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = _fallbackMesh;
            // NavMeshBaker reads collider.bounds for the bake volume; a collider moved this frame reports its old bounds
            // until the transforms are synced, which would clip the disc.
            Physics.SyncTransforms();
            _baker.AddWalkable(collider);
            return true;
        }

        public void ClearFallbackFloor()
        {
            if (_fallback == null)
                return;
            if (_fallback.TryGetComponent<MeshCollider>(out var collider))
                _baker.Remove(collider);
            Destroy(_fallback);
            Destroy(_fallbackMesh);
            _fallback = null;
            _fallbackMesh = null;
        }

        void Awake() => _baker = new NavMeshBaker();

        void OnEnable()
        {
            if (planes != null)
            {
                planes.trackablesChanged.AddListener(OnPlanesChanged);
                foreach (var plane in planes.trackables)
                    UpsertPlane(plane);
            }
            if (boxes != null)
            {
                boxes.trackablesChanged.AddListener(OnBoxesChanged);
                foreach (var box in boxes.trackables)
                    UpsertBox(box);
            }
        }

        void OnDisable()
        {
            if (planes != null)
                planes.trackablesChanged.RemoveListener(OnPlanesChanged);
            if (boxes != null)
                boxes.trackablesChanged.RemoveListener(OnBoxesChanged);
        }

        void Update()
        {
            if (_clock != null)
                _baker.Tick(_clock.Now);
        }

        void OnDestroy()
        {
            foreach (var collider in _colliders.Values)
                collider.Destroy();
            _colliders.Clear();
            _floorPlanes.Clear();
            ClearFallbackFloor();
            _baker.Dispose();
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added)
                UpsertPlane(plane);
            foreach (var plane in args.updated)
                UpsertPlane(plane);
            foreach (var removed in args.removed)
                Remove(removed.Key);
        }

        void OnBoxesChanged(ARTrackablesChangedEventArgs<ARBoundingBox> args)
        {
            foreach (var box in args.added)
                UpsertBox(box);
            foreach (var box in args.updated)
                UpsertBox(box);
            foreach (var removed in args.removed)
                Remove(removed.Key);
        }

        void UpsertPlane(ARPlane plane)
        {
            if ((plane.classifications & PlaneClassifications.Floor) == 0)
            {
                Remove(plane.trackableId);
                return;
            }
            var entry = GetOrCreate(plane.trackableId, plane.transform);
            if (!BuildFloorMesh(plane, entry.Mesh))
                return;
            _floorPlanes.Add(plane.trackableId);
            var collider = entry.GetOrAdd<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = entry.Mesh;
            _baker.AddWalkable(collider);
        }

        void UpsertBox(ARBoundingBox box)
        {
            var entry = GetOrCreate(box.trackableId, box.transform);
            var collider = entry.GetOrAdd<BoxCollider>();
            collider.size = box.size;
            _baker.AddObstacle(collider);
        }

        void Remove(TrackableId id)
        {
            _floorPlanes.Remove(id);
            if (!_colliders.Remove(id, out var entry))
                return;
            if (entry.Collider != null)
                _baker.Remove(entry.Collider);
            entry.Destroy();
        }

        SceneCollider GetOrCreate(TrackableId id, Transform trackable)
        {
            if (!_colliders.TryGetValue(id, out var entry))
            {
                entry = new SceneCollider(trackable, navGeometryLayer);
                _colliders.Add(id, entry);
            }
            return entry;
        }

        bool BuildFloorMesh(ARPlane plane, Mesh mesh)
        {
            _outline.Clear();
            foreach (var point in plane.boundary)
                _outline.Add(point);
            return FloorMesh.TryBuild(_outline, mesh, _vertices, _triangles);
        }
    }
}
