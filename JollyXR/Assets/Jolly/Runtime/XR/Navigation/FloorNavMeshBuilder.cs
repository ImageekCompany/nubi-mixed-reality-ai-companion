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
    /// NavGeometry layer, so IWorldQuery can also raycast them. Rebuilds are throttled by NavMeshBaker.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] ARPlaneManager planes;
        [SerializeField] ARBoundingBoxManager boxes;
        [Tooltip("Layer for the generated floor and furniture colliders (NavGeometry).")]
        [SerializeField] int navGeometryLayer = 8;

        readonly Dictionary<TrackableId, SceneCollider> _colliders = new();
        readonly List<Vector2> _outline = new();
        readonly List<Vector3> _vertices = new();
        readonly List<int> _triangles = new();
        NavMeshBaker _baker;
        IClock _clock;

        public int NavGeometryLayer => navGeometryLayer;
        public bool HasNavMesh => _baker != null && _baker.HasNavMesh;

        public void Construct(IClock clock) => _clock = clock;

        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
        {
            point = default;
            return _baker != null && _baker.TrySampleWalkable(near, radius, out point);
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
