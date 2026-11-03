using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Memory;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Tools
{
    /// <summary>Teach a place, ask, get led there with a point (ADR-0051).</summary>
    public sealed class ShowPlaceToolTests
    {
        static readonly Vector3 Kitchen = new(0f, 1.2f, 5f);

        RecordingCharacterController _character;
        FakeWorldQuery _world;
        FakeUserPoseProvider _user;
        SceneMemory _scene;
        FakeLocalizer _localizer;
        readonly LocomotionSettings _settings = new();

        [SetUp]
        public void SetUp()
        {
            _character = new RecordingCharacterController { Position = Vector3.zero, Forward = Vector3.forward };
            _world = new FakeWorldQuery();
            _user = new FakeUserPoseProvider();
            _scene = new SceneMemory("unused", new FakeSpatialAnchors(), new FakeClock(), new MemoryLimits(), new RecordingLog());
            _localizer = new FakeLocalizer();
            _scene.RememberPlace(new PlaceRecord { Name = "kitchen", Pose = new Pose(Kitchen, Quaternion.identity) });
        }

        ShowPlaceTool Tool() => new(_character, _scene, _world, _localizer,
            new PointToPlace(_character, _world, _settings, new RecordingLog()));

        [Test]
        public void TaughtPlace_WalksShortOfItOnTheFloor_PointsAndSaysThere_EndingTheTurn()
        {
            // Act
            var result = ToolHarness.Run(Tool(), "{\"place\":\"Kitchen\"}");

            // Assert
            Assert.That(_character.MoveTarget, Is.EqualTo(new Vector3(0f, 0f, 4f)), "the stand-off short of it, on the floor");
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Point }));
            Assert.That(result.SpokenText, Is.EqualTo("There!"));
            Assert.That(result.EndsTurn, Is.True);
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("pointing"));
        }

        [Test]
        public void Pointing_TurnsTheArmOntoThePlace_AndLooksAtIt()
        {
            ToolHarness.Run(Tool(), "{\"place\":\"kitchen\"}");

            Assert.That(_character.Spins.Count, Is.EqualTo(1));
            Assert.That(_character.Spins[0], Is.LessThan(-_settings.PointArmYaw),
                "the arm sits to the right of the body, so the body turns left of the place");
            Assert.That(_character.LastLook?.Point, Is.EqualTo(Kitchen));
        }

        [Test]
        public void NearlyThereAlready_PointsWithoutAWalk()
        {
            _character.Position = new Vector3(0f, 0f, 4.2f);

            var result = ToolHarness.Run(Tool(), "{\"place\":\"kitchen\"}");

            Assert.That(_character.MoveTarget, Is.Null);
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Point }));
            Assert.That(result.SpokenText, Is.EqualTo("There!"));
        }

        [Test]
        public void PlacePastTheScannedFloor_WalksTheFurthestReachableWay_ThenPoints()
        {
            // Only the first 2 m of the line can be reached: the kitchen is another room.
            _world.Snap = p => p;
            var character = new FarLimitedCharacter(2f, _character);

            var tool = new ShowPlaceTool(character, _scene, _world, _localizer,
                new PointToPlace(character, _world, _settings, new RecordingLog()));
            var result = ToolHarness.Run(tool, "{\"place\":\"kitchen\"}");

            Assert.That(character.Reached.z, Is.LessThanOrEqualTo(2f).And.GreaterThan(1f));
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Point }));
            Assert.That(result.SpokenText, Is.EqualTo("There!"));
        }

        [Test]
        public void NothingReachable_StillPointsFromHere()
        {
            _character.Reachable = false;

            var result = ToolHarness.Run(Tool(), "{\"place\":\"kitchen\"}");

            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Point }));
            Assert.That(result.SpokenText, Is.EqualTo("There!"));
        }

        [Test]
        public void WalkStoppedOnTheWay_DoesNotPointOrSpeak()
        {
            var character = new StoppedCharacter(_character);
            var tool = new ShowPlaceTool(character, _scene, _world, _localizer,
                new PointToPlace(character, _world, _settings, new RecordingLog()));

            var result = ToolHarness.Run(tool, "{\"place\":\"kitchen\"}");

            Assert.That(_character.Gestures, Is.Empty);
            Assert.That(result.SpokenText, Is.Null);
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("stopped"));
        }

        [Test]
        public void SeenObjectAndFurniture_ResolveLikeWalkTo()
        {
            _scene.AddObject("white mug", new Vector3(3f, 0.8f, 0f), "seen");
            _world.AddBox("couch", new Vector3(-3f, 0f, 0f), new Vector3(1f, 0.8f, 1f));

            ToolHarness.Run(Tool(), "{\"place\":\"mug\"}");
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(3f, 0.8f, 0f)));

            ToolHarness.Run(Tool(), "{\"place\":\"sofa\"}");
            Assert.That(_character.LastLook?.Point.x, Is.EqualTo(-3f));
        }

        [Test]
        public void UnknownPlace_FailsForTheModelToExplain()
        {
            var result = ToolHarness.Run(Tool(), "{\"place\":\"garage\"}");

            Assert.That(result.Ok, Is.False);
            Assert.That(_character.MoveTarget, Is.Null);
            Assert.That(_character.Gestures, Is.Empty);
        }

        [Test]
        public void CharacterWithoutThePointClip_StillWalksAndSpeaks()
        {
            _character.Missing.Add(GestureId.Point);

            var result = ToolHarness.Run(Tool(), "{\"place\":\"kitchen\"}");

            Assert.That(_character.MoveTarget, Is.Not.Null);
            Assert.That(_character.Gestures, Is.Empty);
            Assert.That(result.SpokenText, Is.EqualTo("There!"));
        }

        [Test]
        public void RememberThere_SavesWhereTheGazeLands_AndTurnsToIt()
        {
            _world.WallZ = 6f;
            _user.Head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            var tool = new RememberPlaceTool(_scene, new FakeSpatialAnchors(), _user, _character, new FakeClock(), new RecordingLog(), _world, _settings);

            ToolHarness.Run(tool, "{\"name\":\"pantry\",\"where\":\"there\"}");

            Assert.That(_scene.TryGetPlace("pantry", out var place), Is.True);
            Assert.That(place.Pose.position, Is.EqualTo(new Vector3(0f, 1.6f, 6f)));
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(0f, 1.6f, 6f)));
        }

        [Test]
        public void RememberThere_PointingRayBeatsTheGaze()
        {
            _world.WallZ = 6f;
            _user.Head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            _user.PointingRay = new Ray(new Vector3(1f, 1f, 0f), Vector3.forward);
            var tool = new RememberPlaceTool(_scene, new FakeSpatialAnchors(), _user, _character, new FakeClock(), new RecordingLog(), _world, _settings);

            ToolHarness.Run(tool, "{\"name\":\"pantry\",\"where\":\"there\"}");

            _scene.TryGetPlace("pantry", out var place);
            Assert.That(place.Pose.position, Is.EqualTo(new Vector3(1f, 1f, 6f)));
        }

        [Test]
        public void RememberThere_RayHitsNothing_LandsAtTheFallbackDistance()
        {
            _user.Head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            var tool = new RememberPlaceTool(_scene, new FakeSpatialAnchors(), _user, _character, new FakeClock(), new RecordingLog(), _world, _settings);

            ToolHarness.Run(tool, "{\"name\":\"pantry\",\"where\":\"there\"}");

            _scene.TryGetPlace("pantry", out var place);
            Assert.That(place.Pose.position, Is.EqualTo(new Vector3(0f, 1.6f, _settings.ShownPlaceFallbackDistance)));
        }

        [Test]
        public void WalkTo_PlaceRememberedUpAWall_SamplesTheFloorBelowIt()
        {
            ToolHarness.Run(new WalkToTool(_character, _scene, _world, _settings), "{\"place\":\"kitchen\"}");

            Assert.That(_world.LastSampleNear?.y, Is.EqualTo(0f));
            Assert.That(_character.MoveTarget, Is.EqualTo(new Vector3(0f, 0f, 5f)));
        }

        /// <summary>Reaches only targets within a distance, as a NavMesh that ends at the scanned room does.</summary>
        sealed class FarLimitedCharacter : ICharacterController
        {
            readonly float _limit;
            readonly RecordingCharacterController _inner;
            public FarLimitedCharacter(float limit, RecordingCharacterController inner) { _limit = limit; _inner = inner; }
            public Vector3 Reached { get; private set; }
            public Vector3 Position => _inner.Position;
            public Vector3 Forward => _inner.Forward;
            public bool IsMoving => _inner.IsMoving;
            public LocomotionMode Mode => _inner.Mode;
            public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds)
            {
                etaSeconds = 0f;
                if (Vector3.Distance(worldTarget, Position) > _limit)
                    return false;
                Reached = worldTarget;
                return _inner.TryStartMoveTo(worldTarget, stopDistance, out etaSeconds);
            }
            public bool TryStartMoveVia(Vector3 waypoint, Vector3 worldTarget, float stopDistance, float minUserDistance, out float etaSeconds) =>
                _inner.TryStartMoveVia(waypoint, worldTarget, stopDistance, minUserDistance, out etaSeconds);
            public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) => _inner.WhenArrived(ct);
            public void Follow(float distanceMin, float distanceMax) => _inner.Follow(distanceMin, distanceMax);
            public void Stop() => _inner.Stop();
            public void Relocate(Vector3 floorPoint) => _inner.Relocate(floorPoint);
            public void LookAt(LookTarget target) => _inner.LookAt(target);
            public UniTask PlayGesture(GestureId gesture, CancellationToken ct) => _inner.PlayGesture(gesture, ct);
            public UniTask PlayLooped(GestureId gesture, float seconds, CancellationToken ct) => _inner.PlayLooped(gesture, seconds, ct);
            public bool CanPlay(GestureId gesture) => _inner.CanPlay(gesture);
            public UniTask Spin(float degrees, CancellationToken ct) => _inner.Spin(degrees, ct);
            public void SetMood(Mood mood) => _inner.SetMood(mood);
            public void SetConversationState(ConversationState state) => _inner.SetConversationState(state);
        }

        /// <summary>A started walk that a stop command ends before it arrives.</summary>
        sealed class StoppedCharacter : ICharacterController
        {
            readonly RecordingCharacterController _inner;
            public StoppedCharacter(RecordingCharacterController inner) => _inner = inner;
            public Vector3 Position => _inner.Position;
            public Vector3 Forward => _inner.Forward;
            public bool IsMoving => _inner.IsMoving;
            public LocomotionMode Mode => _inner.Mode;
            public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds) =>
                _inner.TryStartMoveTo(worldTarget, stopDistance, out etaSeconds);
            public bool TryStartMoveVia(Vector3 waypoint, Vector3 worldTarget, float stopDistance, float minUserDistance, out float etaSeconds) =>
                _inner.TryStartMoveVia(waypoint, worldTarget, stopDistance, minUserDistance, out etaSeconds);
            public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) => UniTask.FromResult(MoveOutcome.Cancelled);
            public void Follow(float distanceMin, float distanceMax) => _inner.Follow(distanceMin, distanceMax);
            public void Stop() => _inner.Stop();
            public void Relocate(Vector3 floorPoint) => _inner.Relocate(floorPoint);
            public void LookAt(LookTarget target) => _inner.LookAt(target);
            public UniTask PlayGesture(GestureId gesture, CancellationToken ct) => _inner.PlayGesture(gesture, ct);
            public UniTask PlayLooped(GestureId gesture, float seconds, CancellationToken ct) => _inner.PlayLooped(gesture, seconds, ct);
            public bool CanPlay(GestureId gesture) => _inner.CanPlay(gesture);
            public UniTask Spin(float degrees, CancellationToken ct) => _inner.Spin(degrees, ct);
            public void SetMood(Mood mood) => _inner.SetMood(mood);
            public void SetConversationState(ConversationState state) => _inner.SetConversationState(state);
        }
    }
}
