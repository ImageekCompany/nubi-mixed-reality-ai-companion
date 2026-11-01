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
    public sealed class LocomotionToolTests
    {
        RecordingCharacterController _character;
        FakeWorldQuery _world;
        FakeUserPoseProvider _user;
        SceneMemory _scene;
        readonly LocomotionSettings _settings = new();

        [SetUp]
        public void SetUp()
        {
            _character = new RecordingCharacterController { Position = new Vector3(0, 0.02f, 0) };
            _world = new FakeWorldQuery();
            _user = new FakeUserPoseProvider();
            _scene = new SceneMemory("unused", new FakeSpatialAnchors(), new FakeClock(), new MemoryLimits(), new RecordingLog());
        }

        WalkToTool WalkTo() => new(_character, _scene, _world, _settings);

        [Test]
        public void WalkTo_TaughtPlace_StartsMotionAndReturnsAtOnce()
        {
            _scene.RememberPlace(new PlaceRecord { Name = "sofa", Pose = new Pose(new Vector3(2, 0, 3), Quaternion.identity) });

            var result = ToolHarness.Run(WalkTo(), "{\"place\":\"Sofa\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"walking\",\"place\":\"Sofa\",\"eta_s\":4}"));
            Assert.That(_character.MoveTarget, Is.EqualTo(new Vector3(2, 0, 3)));
            Assert.That(_character.MoveStopDistance, Is.EqualTo(_settings.WalkStopDistance));
            Assert.That(result.EndsTurn, Is.False);
        }

        [Test]
        public void WalkTo_FurnitureWord_StopsBesideTheSurfaceFacingIt()
        {
            _world.AddBox("couch", new Vector3(0, 0, 2), new Vector3(2f, 0.8f, 1f));

            var result = ToolHarness.Run(WalkTo(), "{\"place\":\"sofa\"}");

            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(0, 0, 1.25f)), Is.LessThan(1e-4f),
                "the near edge plus clearance, on the floor, never the seat");
            Assert.That(_character.MoveStopDistance, Is.EqualTo(_settings.SurfaceStopDistance));
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(0, 0.8f, 2)));
        }

        [Test]
        public void WalkTo_SeenObjectId()
        {
            var seen = _scene.AddObject("mug", new Vector3(-1, 0.8f, 2), "s");

            ToolHarness.Run(WalkTo(), "{\"place\":\"" + seen.Id + "\"}");

            Assert.That(_character.MoveTarget, Is.EqualTo(new Vector3(-1, 0, 2)));
        }

        [Test]
        public void WalkTo_UnknownPlace_FailsForTheModelToAskForTeaching()
        {
            var result = ToolHarness.Run(WalkTo(), "{\"place\":\"kitchen\"}");

            Assert.That(result.Ok, Is.False);
            Assert.That((string)ToolHarness.Output(result)["error"], Is.EqualTo("unknown_place"));
            Assert.That(_character.MoveTarget, Is.Null);
        }

        [Test]
        public void WalkTo_SeenObjectLabel_WalksToTheMostRecentMatch()
        {
            _scene.AddObject("white enamel mug", new Vector3(-1, 0.8f, 2), "s");
            _scene.AddObject("blue mug", new Vector3(2, 0.8f, 2), "s");

            var result = ToolHarness.Run(WalkTo(), "{\"place\":\"Mug\"}");

            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(_character.MoveTarget, Is.EqualTo(new Vector3(2, 0, 2)));
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(2, 0.8f, 2)), "faces what it walks to");
        }

        [Test]
        public void WalkTo_SeenObjectLabel_ExactMatchBeatsANewerWordMatch()
        {
            _scene.AddObject("mug", new Vector3(-1, 0.8f, 2), "s");
            _scene.AddObject("mug handle", new Vector3(2, 0.8f, 2), "s");

            ToolHarness.Run(WalkTo(), "{\"place\":\"mug\"}");

            Assert.That(_character.MoveTarget, Is.EqualTo(new Vector3(-1, 0, 2)));
        }

        [Test]
        public void WalkTo_PartialWord_IsNotALabelMatch()
        {
            _scene.AddObject("mug", new Vector3(-1, 0.8f, 2), "s");

            var result = ToolHarness.Run(WalkTo(), "{\"place\":\"mu\"}");

            Assert.That((string)ToolHarness.Output(result)["error"], Is.EqualTo("unknown_place"));
        }

        [Test]
        public void WalkTo_Unreachable_LooksTowardTheTarget()
        {
            _world.AddBox("table", new Vector3(3, 0, 0), new Vector3(1f, 0.7f, 1f));
            _character.Reachable = false;

            var result = ToolHarness.Run(WalkTo(), "{\"place\":\"table\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"unreachable\",\"place\":\"table\"}"));
            Assert.That(_character.LastLook?.Kind, Is.EqualTo(LookTargetKind.WorldPoint));
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(3, 0.7f, 0)));
        }

        [Test]
        public void ComeHere_TargetsOneMeterInFrontOfTheUserOnNubisFloor()
        {
            _user.Head = new Pose(new Vector3(1, 1.6f, 1), Quaternion.Euler(30, 90, 0));

            var result = ToolHarness.Run(new ComeHereTool(_character, _user, _world, _settings));

            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(2, 0, 1)), Is.LessThan(1e-4f));
            Assert.That(_character.MoveStopDistance, Is.EqualTo(_settings.ComeHereStopDistance));
            Assert.That(_character.LastLook?.Kind, Is.EqualTo(LookTargetKind.User));
        }

        [Test]
        public void MovementTools_DescriptionsSayTheyNeedTheUsersRequest()
        {
            // A greeting ("Hi Nubi, who are you?") called come_here in 4 of 7 device runs (selftest-report.md, 4d228c8).
            var come = new ComeHereTool(_character, _user, _world, _settings).Definition.Description;
            var follow = new FollowUserTool(_character, _settings).Definition.Description;

            Assert.That(come, Does.Contain("only when the user asks").And.Contain("never for a greeting"));
            Assert.That(follow, Does.Contain("only when the user asks"));
        }

        [Test]
        public void ComeHere_NoWalkableFloor_IsUnreachable()
        {
            _world.Walkable = false;

            var result = ToolHarness.Run(new ComeHereTool(_character, _user, _world, _settings));

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"unreachable\"}"));
            Assert.That(_character.MoveTarget, Is.Null);
        }

        [Test]
        public void FollowUser_StartsFollowModeWithComfortDistances()
        {
            var tool = new FollowUserTool(_character, _settings);

            var result = ToolHarness.Run(tool);

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"following\"}"));
            Assert.That(_character.FollowRange, Is.EqualTo((1.0f, 1.5f)));
            Assert.That(tool.Definition.Concurrency, Is.EqualTo(ToolConcurrency.Locomotion));
        }
    }
}
