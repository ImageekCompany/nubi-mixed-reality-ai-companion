#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.XR.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// The real Nubi and the production NavMesh bake in a simulated scanned room: a couch box carved out of the floor
    /// (ADR-0024). go_to_surface must bring Nubi to the floor beside the couch, facing it.
    /// </summary>
    public sealed class GoToSurfaceTests
    {
        static readonly Vector3 CouchFloor = new(0.5f, 0f, -2.5f);
        static readonly Vector3 CouchSize = new(2f, 0.8f, 0.9f);

        CharacterTestRig _rig;
        RigWorld _world;

        /// <summary>IWorldQuery over the rig's NavMeshBaker and a fixed list of scanned surfaces.</summary>
        sealed class RigWorld : IWorldQuery
        {
            readonly NavMeshBaker _navMesh;
            public RigWorld(NavMeshBaker navMesh) => _navMesh = navMesh;
            public List<SceneSurface> Surfaces { get; } = new();
            public WalkableArea WalkableArea => WalkableArea.Scanned;
            public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
            {
                hit = default;
                return false;
            }
            public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point) =>
                _navMesh.TrySampleWalkable(near, radius, out point);
            public void GetSurfaces(List<SceneSurface> into)
            {
                into.Clear();
                into.AddRange(Surfaces);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _rig = CharacterTestRig.Create();
            _rig.AddFurniture(CouchFloor, CouchSize);
            _world = new RigWorld(_rig.NavMesh);
            _world.Surfaces.Add(new SceneSurface("couch",
                new Pose(CouchFloor + Vector3.up * (CouchSize.y * 0.5f), Quaternion.identity), CouchSize));
        }

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [UnityTest]
        public IEnumerator GoToSurface_WalksToTheCouchEdgeAndFacesIt() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var tool = new GoToSurfaceTool(_rig.Nubi, _world, new FakeRoomScan(), new LocomotionSettings());
            var registry = new ToolRegistry();
            registry.Register(tool);
            Assert.That(registry.TryValidate(ToolNames.GoToSurface, "{\"label\":\"couch\"}", out var args, out var error), Is.True, error);

            // Act
            var result = await tool.Execute(args, new ToolContext(), CancellationToken.None);
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None);
            await CharacterTestRig.Until(() => false, 1f);

            // Assert
            Assert.That(result.OutputJson, Does.StartWith("{\"status\":\"walking\",\"label\":\"couch\""));
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
            var nearEdge = CouchFloor.z + CouchSize.z * 0.5f;
            var position = _rig.Nubi.Position;
            Assert.That(position.z - nearEdge, Is.InRange(0.1f, 0.45f), "on the floor just in front of the couch");
            Assert.That(Mathf.Abs(position.x), Is.LessThan(0.3f), "the edge point nearest to where Nubi started (x = 0)");
            Assert.That(position.y, Is.EqualTo(0f).Within(0.1f), "never on the seat");
            var toCouch = Vector3.ProjectOnPlane(CouchFloor - position, Vector3.up).normalized;
            Assert.That(Vector3.Dot(_rig.Nubi.Forward, toCouch), Is.GreaterThan(0.7f), "facing the couch");
        });

        [UnityTest]
        public IEnumerator GoToSurface_FromBehindTheCouch_StopsAtItsBackEdge() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            _rig.Nubi.transform.position = new Vector3(0.5f, 0f, -4f);
            var tool = new GoToSurfaceTool(_rig.Nubi, _world, new FakeRoomScan(), new LocomotionSettings());
            var registry = new ToolRegistry();
            registry.Register(tool);
            registry.TryValidate(ToolNames.GoToSurface, "{\"label\":\"couch\"}", out var args, out _);

            // Act
            await tool.Execute(args, new ToolContext(), CancellationToken.None);
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None);

            // Assert
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
            var farEdge = CouchFloor.z - CouchSize.z * 0.5f;
            Assert.That(farEdge - _rig.Nubi.Position.z, Is.InRange(0.1f, 0.45f), "the edge nearest to where Nubi stood");
        });
    }
}
#endif
