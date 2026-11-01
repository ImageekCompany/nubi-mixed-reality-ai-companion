using System.Collections;
using System.Collections.Generic;
using Jolly.XR.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>A simulated Space Setup room: a 6 x 6 m floor slab and a 0.8 x 0.5 x 0.8 m table box.</summary>
    public sealed class NavMeshBakerTests
    {
        const int Layer = 30;

        GameObject _root;
        NavMeshBaker _baker;
        BoxCollider _floor;
        BoxCollider _table;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("room");
            _floor = AddBox("floor", new Vector3(0f, -0.01f, 0f), new Vector3(6f, 0.02f, 6f));
            _table = AddBox("table", new Vector3(1f, 0.25f, 1f), new Vector3(0.8f, 0.5f, 0.8f));
            _baker = new NavMeshBaker();
        }

        [TearDown]
        public void TearDown()
        {
            _baker.Dispose();
            Object.Destroy(_root);
        }

        BoxCollider AddBox(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(_root.transform);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        static bool OnNavMesh(Vector3 point, float radius, out Vector3 hit)
        {
            var found = NavMesh.SamplePosition(point, out var navHit, radius, NavMesh.AllAreas);
            hit = navHit.position;
            return found;
        }

        [UnityTest]
        public IEnumerator Build_FloorIsWalkable()
        {
            _baker.AddWalkable(_floor);
            yield return null;

            _baker.Rebuild(now: 0);

            Assert.That(_baker.HasNavMesh, Is.True);
            Assert.That(OnNavMesh(new Vector3(-1.5f, 0f, -1.5f), 0.2f, out var hit), Is.True);
            Assert.That(hit.y, Is.EqualTo(0f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator Build_ObstacleTop_IsNotWalkable()
        {
            _baker.AddWalkable(_floor);
            _baker.AddObstacle(_table);
            yield return null;

            _baker.Rebuild(now: 0);

            Assert.That(OnNavMesh(new Vector3(1f, 0.5f, 1f), 0.3f, out _), Is.False, "box tops never bake as islands");
            Assert.That(OnNavMesh(new Vector3(1f, 0f, 1f), 0.3f, out _), Is.False, "the floor under the box is carved");
        }

        [UnityTest]
        public IEnumerator Build_SameBoxAsWalkableGeometry_WouldBakeItsTop()
        {
            _baker.AddWalkable(_floor);
            _baker.AddWalkable(_table);
            yield return null;

            _baker.Rebuild(now: 0);

            Assert.That(OnNavMesh(new Vector3(1f, 0.5f, 1f), 0.3f, out var hit), Is.True, "control: the obstacle marking is what matters");
            Assert.That(hit.y, Is.EqualTo(0.5f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator TrySampleWalkable_FromATableTop_ReturnsAFloorPointBesideIt()
        {
            _baker.AddWalkable(_floor);
            _baker.AddObstacle(_table);
            yield return null;
            _baker.Rebuild(now: 0);

            Assert.That(_baker.TrySampleWalkable(new Vector3(1f, 0.5f, 1f), 1.5f, out var point), Is.True);

            Assert.That(point.y, Is.EqualTo(0f).Within(0.1f));
            Assert.That(_table.bounds.Contains(new Vector3(point.x, 0.25f, point.z)), Is.False);
        }

        [UnityTest]
        public IEnumerator MarkDirty_RebuildsAtMostEveryTwoSeconds()
        {
            _baker.AddWalkable(_floor);
            yield return null;
            _baker.Tick(now: 0);
            Assert.That(_baker.HasNavMesh, Is.True, "the first build is immediate");

            _baker.AddObstacle(_table);
            _baker.Tick(now: 1.0);
            Assert.That(OnNavMesh(new Vector3(1f, 0f, 1f), 0.3f, out _), Is.True, "throttled: still the old mesh");

            _baker.Tick(now: 2.1);
            Assert.That(OnNavMesh(new Vector3(1f, 0f, 1f), 0.3f, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator Build_TriangulatedLShapedFloorMesh_LeavesTheNotchUnwalkable()
        {
            // 4 x 4 m with the +x +z 2 x 2 m quadrant cut out, clockwise like an ARPlane boundary.
            var outline = new List<Vector2>
            {
                new(-2f, -2f), new(-2f, 2f), new(0f, 2f), new(0f, 0f), new(2f, 0f), new(2f, -2f),
            };
            var mesh = new Mesh();
            Assert.That(FloorMesh.TryBuild(outline, mesh, new List<Vector3>(), new List<int>()), Is.True);
            var go = new GameObject("floor-mesh") { layer = Layer };
            go.transform.SetParent(_root.transform);
            var floor = go.AddComponent<MeshCollider>();
            floor.sharedMesh = mesh;
            _baker.AddWalkable(floor);
            yield return null;

            _baker.Rebuild(now: 0);

            Assert.That(OnNavMesh(new Vector3(-1f, 0f, 1f), 0.1f, out var inside), Is.True, "the L's arm is walkable");
            Assert.That(inside.y, Is.EqualTo(0f).Within(0.1f));
            Assert.That(OnNavMesh(new Vector3(-1f, 0f, -1f), 0.1f, out _), Is.True);
            Assert.That(OnNavMesh(new Vector3(1.2f, 0f, 1.2f), 0.5f, out _), Is.False, "the notch is outside the room");
            Object.Destroy(mesh);
        }

        [UnityTest]
        public IEnumerator Remove_LastWalkable_ClearsTheNavMesh()
        {
            _baker.AddWalkable(_floor);
            yield return null;
            _baker.Rebuild(now: 0);

            _baker.Remove(_floor);
            _baker.Rebuild(now: 1);

            Assert.That(_baker.HasNavMesh, Is.False);
            Assert.That(OnNavMesh(Vector3.zero, 0.5f, out _), Is.False);
        }
    }
}
