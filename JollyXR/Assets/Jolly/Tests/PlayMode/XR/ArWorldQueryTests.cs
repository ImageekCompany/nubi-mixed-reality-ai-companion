using System.Collections;
using Jolly.XR.Navigation;
using Jolly.XR.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>Without AR managers (Editor, or before USE_SCENE): only NavGeometry colliders answer.</summary>
    public sealed class ArWorldQueryTests
    {
        GameObject _root;
        ArWorldQuery _query;
        FloorNavMeshBuilder _floor;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("world");
            _floor = _root.AddComponent<FloorNavMeshBuilder>();
            _query = new ArWorldQuery(null, null, null, _floor);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        GameObject AddWall(int layer)
        {
            var wall = new GameObject("wall") { layer = layer };
            wall.transform.SetParent(_root.transform);
            wall.transform.position = new Vector3(0f, 1f, 2f);
            wall.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.1f);
            return wall;
        }

        [UnityTest]
        public IEnumerator TryRaycast_HitsNavGeometryColliders()
        {
            AddWall(_floor.NavGeometryLayer);
            yield return new WaitForFixedUpdate();

            Assert.That(_query.TryRaycast(new Ray(new Vector3(0f, 1f, 0f), Vector3.forward), 5f, out var hit), Is.True);
            Assert.That(hit.z, Is.EqualTo(1.95f).Within(1e-3f));
            Assert.That(_query.TryRaycast(new Ray(new Vector3(0f, 1f, 0f), Vector3.forward), 1f, out _), Is.False, "beyond maxDistance");
        }

        [UnityTest]
        public IEnumerator TryRaycast_IgnoresOtherLayers()
        {
            AddWall(0);
            yield return new WaitForFixedUpdate();

            Assert.That(_query.TryRaycast(new Ray(new Vector3(0f, 1f, 0f), Vector3.forward), 5f, out _), Is.False);
        }

        [Test]
        public void WithoutSceneData_NothingIsWalkableOrLabelled()
        {
            Assert.That(_query.HasWalkableArea, Is.False);
            Assert.That(_query.TrySampleWalkable(Vector3.zero, 1f, out _), Is.False);
            Assert.That(_query.TryFindLabeledSurface("couch", Vector3.zero, out _), Is.False);
        }
    }
}
