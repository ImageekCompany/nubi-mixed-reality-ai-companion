using System;
using System.Collections.Generic;
using Jolly.App.SelfTest;
using Jolly.App.SelfTest.Scenarios;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Spatial;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.App
{
    /// <summary>The tap_to_move scenario's choice of point: only a ray the real resolver takes for floor proves a walk.</summary>
    public sealed class TapToMovePickTests
    {
        /// <summary>A headset on a desk: rays <see cref="Blocked"/> says so meet the desk, the rest reach no geometry.</summary>
        sealed class DeskWorld : IWorldQuery
        {
            public Func<Ray, bool> Blocked = _ => false;
            public WalkableArea WalkableArea => WalkableArea.Scanned;

            public bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit)
            {
                hit = ray.GetPoint(0.3f);
                return Blocked(ray);
            }

            public bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point)
            {
                point = new Vector3(near.x, 0f, near.z);
                return true;
            }

            public void GetSurfaces(List<SceneSurface> into) => into.Clear();
        }

        static readonly Vector3 Head = new(0f, 0.71f, 0f);
        static readonly Vector3 CharacterInFront = new(0f, 0f, 1.2f);

        DeskWorld _world;
        MoveTargetResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _world = new DeskWorld();
            var character = new RecordingCharacterController { Position = CharacterInFront };
            var user = new FakeUserPoseProvider { Head = new Pose(Head, Quaternion.identity) };
            _resolver = new MoveTargetResolver(_world, character, user, new PointMoveSettings(), new LocomotionSettings());
        }

        [Test]
        public void TryPickPoint_NearestRayBlocked_PicksARayThatResolvesToFloor()
        {
            // Arrange: everything on the character's side of the user meets the desk.
            _world.Blocked = ray => ray.direction.z > -0.01f;

            // Act
            var picked = TapToMoveScenario.TryPickPoint(_world, _resolver, Head, CharacterInFront, out var point, out var blocked);

            // Assert
            Assert.That(picked, Is.True);
            Assert.That(blocked, Is.GreaterThan(0));
            Assert.That(_resolver.TryResolve(new Ray(Head, point - Head), out var target), Is.True, "the pinch will find this target");
            Assert.That(target.Kind, Is.EqualTo(MoveTargetKind.Floor));
        }

        [Test]
        public void TryPickPoint_EveryRayBlocked_PicksNothingAndSaysSo()
        {
            // Arrange
            _world.Blocked = _ => true;

            // Act
            var picked = TapToMoveScenario.TryPickPoint(_world, _resolver, Head, CharacterInFront, out _, out var blocked);

            // Assert
            Assert.That(picked, Is.False);
            Assert.That(blocked, Is.EqualTo(12));
        }

        [TestCase(0.71f, ScenarioStatus.Degraded, TestName = "ReportBlockedRays_HeadsetOnADesk_Degrades")]
        [TestCase(0.99f, ScenarioStatus.Degraded, TestName = "ReportBlockedRays_JustUnderWornHeight_Degrades")]
        [TestCase(1.0f, ScenarioStatus.Fail, TestName = "ReportBlockedRays_AtWornHeight_Fails")]
        [TestCase(1.15f, ScenarioStatus.Fail, TestName = "ReportBlockedRays_Seated_Fails")]
        [TestCase(1.65f, ScenarioStatus.Fail, TestName = "ReportBlockedRays_Standing_Fails")]
        public void ReportBlockedRays_OnlyAHeadsetNotWornExcusesIt(float headAboveFloor, ScenarioStatus expected)
        {
            // Arrange
            var r = new ScenarioResult("t");

            // Act
            TapToMoveScenario.ReportBlockedRays(r, headAboveFloor);

            // Assert
            Assert.That(r.Status, Is.EqualTo(expected));
        }

        [Test]
        public void TryPickPoint_NothingInTheWay_PicksTheFloorNearest15MFromTheCharacter()
        {
            // Act
            var picked = TapToMoveScenario.TryPickPoint(_world, _resolver, Head, CharacterInFront, out var point, out var blocked);

            // Assert
            Assert.That(picked, Is.True);
            Assert.That(blocked, Is.Zero);
            var distance = new Vector2(point.x - CharacterInFront.x, point.z - CharacterInFront.z).magnitude;
            Assert.That(distance, Is.EqualTo(1.5f).Within(0.2f));
        }
    }
}
