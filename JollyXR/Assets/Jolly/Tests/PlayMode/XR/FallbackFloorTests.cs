using System;
using System.Collections;
using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Spatial;
using Jolly.Testing;
using Jolly.XR.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    /// <summary>An unscanned room (ADR-0025): no AR managers, only the fallback disc the room-scan flow asks for.</summary>
    public sealed class FallbackFloorTests
    {
        const float Radius = 1.5f;

        readonly List<Vector2> _outline = new();
        GameObject _root;
        FloorNavMeshBuilder _floor;
        FakeClock _clock;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("room");
            _floor = _root.AddComponent<FloorNavMeshBuilder>();
            _clock = new FakeClock();
            _floor.Construct(_clock);
            FallbackFloorArea.BuildOutline(Radius, 24, _outline);
        }

        [TearDown]
        public void TearDown() => Object.Destroy(_root);

        static bool PathExists(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.SamplePosition(from, out var start, 0.2f, NavMesh.AllAreas)
                   && NavMesh.SamplePosition(to, out var end, 0.2f, NavMesh.AllAreas)
                   && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                   && path.status == NavMeshPathStatus.PathComplete;
        }

        [UnityTest]
        public IEnumerator SetFallbackFloor_BakesAWalkableDiscAtTheFloorHeight()
        {
            var center = new Vector3(1f, 0f, 2f);

            Assert.That(_floor.SetFallbackFloor(center, _outline, floorHeight: 0.05f), Is.True);
            yield return null;

            Assert.That(_floor.WalkableArea, Is.EqualTo(WalkableArea.Fallback));
            Assert.That(_floor.HasScannedFloor, Is.False);
            Assert.That(_floor.TrySampleWalkable(center + new Vector3(0f, 1.6f, 1f), 0.3f, out var point), Is.True,
                "a point 1 m from the user, sampled from head height");
            Assert.That(point.y, Is.EqualTo(0.05f).Within(0.1f));
            Assert.That(PathExists(center + new Vector3(0f, 0f, 1.2f), center + new Vector3(0f, 0f, -1.2f)), Is.True,
                "Nubi can cross the disc to come closer");
            Assert.That(_floor.TrySampleWalkable(center + new Vector3(0f, 0f, 2.5f), 0.3f, out _), Is.False,
                "nothing beyond the disc");
        }

        [UnityTest]
        public IEnumerator SetFallbackFloor_Again_MovesTheDiscOnTheNextAllowedRebuild()
        {
            _floor.SetFallbackFloor(Vector3.zero, _outline, 0f);
            yield return null;

            _floor.SetFallbackFloor(new Vector3(3f, 0f, 0f), _outline, 0f);
            _clock.Advance(TimeSpan.FromSeconds(2.1));
            yield return null;

            Assert.That(_floor.TrySampleWalkable(new Vector3(4.2f, 0f, 0f), 0.2f, out _), Is.True);
            Assert.That(_floor.TrySampleWalkable(new Vector3(-1.2f, 0f, 0f), 0.2f, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ClearFallbackFloor_LeavesNoWalkableArea()
        {
            _floor.SetFallbackFloor(Vector3.zero, _outline, 0f);
            yield return null;

            _floor.ClearFallbackFloor();
            _clock.Advance(TimeSpan.FromSeconds(2.1));
            yield return null;

            Assert.That(_floor.HasFallbackFloor, Is.False);
            Assert.That(_floor.WalkableArea, Is.EqualTo(WalkableArea.None));
            Assert.That(_floor.TrySampleWalkable(Vector3.zero, 0.5f, out _), Is.False);
        }

        [Test]
        public void SetFallbackFloor_DegenerateOutline_IsRefused()
        {
            var line = new List<Vector2> { Vector2.zero, Vector2.right, Vector2.right * 2f };

            Assert.That(_floor.SetFallbackFloor(Vector3.zero, line, 0f), Is.False);
            Assert.That(_floor.HasFallbackFloor, Is.False);
        }
    }
}
