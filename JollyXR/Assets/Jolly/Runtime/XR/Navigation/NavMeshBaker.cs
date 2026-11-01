using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Jolly.XR.Navigation
{
    /// <summary>
    /// Runtime NavMesh over registered floor and furniture colliders. Furniture is baked with the Not Walkable area
    /// (what a NavMeshModifier with overrideArea does), so it carves the floor and its top never becomes an island.
    /// Built with NavMeshBuilder rather than NavMeshSurface because the surface takes agent settings from the project's
    /// agent type (Humanoid: 0.5 m radius, 0.75 m climb), which would erase a living room; Nubi is ~0.6 m tall.
    /// </summary>
    public sealed class NavMeshBaker : IDisposable
    {
        const double MinRebuildIntervalSeconds = 2.0;
        const float FloorToleranceMeters = 0.1f;
        const int WalkableArea = 0;
        const int NotWalkableArea = 1;

        readonly Dictionary<Collider, int> _colliders = new();
        readonly List<NavMeshBuildSource> _sources = new();
        NavMeshBuildSettings _settings;
        NavMeshData _data;
        NavMeshDataInstance _instance;
        bool _dirty;
        double _lastBuild = double.NegativeInfinity;

        public NavMeshBaker()
        {
            _settings = NavMesh.GetSettingsByID(0);
            _settings.agentRadius = 0.15f;
            _settings.agentHeight = 0.7f;
            _settings.agentClimb = 0.04f;
            _settings.agentSlope = 20f;
            _settings.minRegionArea = 0.1f;
        }

        public bool HasNavMesh => _instance.valid;
        /// <summary>Top of the lowest walkable collider (the floor plane), or 0 before any floor is known.</summary>
        public float FloorHeight { get; private set; }

        public void AddWalkable(Collider collider) => Set(collider, WalkableArea);
        public void AddObstacle(Collider collider) => Set(collider, NotWalkableArea);

        public void Remove(Collider collider)
        {
            if (_colliders.Remove(collider))
                _dirty = true;
        }

        /// <summary>Call after moving or reshaping a registered collider.</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>Rebuilds when dirty: at once if there is no NavMesh yet, otherwise at most every 2 s.</summary>
        public void Tick(double now)
        {
            if (_dirty && (!HasNavMesh || now - _lastBuild >= MinRebuildIntervalSeconds))
                Rebuild(now);
        }

        public void Rebuild(double now)
        {
            _dirty = false;
            _lastBuild = now;
            if (!CollectSources(out var bounds))
            {
                Clear();
                return;
            }

            var data = NavMeshBuilder.BuildNavMeshData(_settings, _sources, bounds, Vector3.zero, Quaternion.identity);
            Clear();
            _data = data;
            _instance = NavMesh.AddNavMeshData(_data);
        }

        /// <summary>NavMesh.SamplePosition, keeping only points within 0.1 m of the floor; a sample that lands elsewhere
        /// is retried from the same spot at floor height.</summary>
        public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
        {
            point = default;
            if (!HasNavMesh)
                return false;
            if (TrySample(near, radius, out point))
                return true;
            return TrySample(new Vector3(near.x, FloorHeight, near.z), radius, out point);
        }

        public void Dispose()
        {
            Clear();
            _colliders.Clear();
        }

        bool TrySample(Vector3 near, float radius, out Vector3 point)
        {
            var found = NavMesh.SamplePosition(near, out var hit, radius, NavMesh.AllAreas)
                        && Mathf.Abs(hit.position.y - FloorHeight) <= FloorToleranceMeters;
            point = hit.position;
            return found;
        }

        void Set(Collider collider, int area)
        {
            _colliders[collider] = area;
            _dirty = true;
        }

        bool CollectSources(out Bounds bounds)
        {
            _sources.Clear();
            bounds = default;
            var floor = float.PositiveInfinity;
            foreach (var (collider, area) in _colliders)
            {
                if (collider == null || !TryCreateSource(collider, area, out var source))
                    continue;
                var colliderBounds = collider.bounds;
                if (_sources.Count == 0)
                    bounds = colliderBounds;
                else
                    bounds.Encapsulate(colliderBounds);
                if (area == WalkableArea)
                    floor = Mathf.Min(floor, colliderBounds.max.y);
                _sources.Add(source);
            }
            if (float.IsPositiveInfinity(floor))
                return false;
            FloorHeight = floor;
            bounds.Expand(1f);
            return true;
        }

        static bool TryCreateSource(Collider collider, int area, out NavMeshBuildSource source)
        {
            source = new NavMeshBuildSource { area = area, component = collider };
            switch (collider)
            {
                case BoxCollider box:
                    source.shape = NavMeshBuildSourceShape.Box;
                    source.size = box.size;
                    source.transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center);
                    return true;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    source.shape = NavMeshBuildSourceShape.Mesh;
                    source.sourceObject = mesh.sharedMesh;
                    source.transform = mesh.transform.localToWorldMatrix;
                    return true;
                default:
                    return false;
            }
        }

        void Clear()
        {
            if (_instance.valid)
                _instance.Remove();
            _instance = default;
            if (_data != null)
                UnityEngine.Object.Destroy(_data);
            _data = null;
        }
    }
}
