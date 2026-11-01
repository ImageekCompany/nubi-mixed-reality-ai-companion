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
    /// <summary>go_to_surface, request_room_scan and look_at on room furniture (ADR-0025).</summary>
    public sealed class RoomToolTests
    {
        RecordingCharacterController _character;
        FakeWorldQuery _world;
        FakeRoomScan _room;
        FakeLocalizer _localizer;
        SceneMemory _scene;
        readonly LocomotionSettings _settings = new();

        [SetUp]
        public void SetUp()
        {
            _character = new RecordingCharacterController { Position = Vector3.zero };
            _world = new FakeWorldQuery();
            _room = new FakeRoomScan();
            _localizer = new FakeLocalizer();
            _scene = new SceneMemory("unused", new FakeSpatialAnchors(), new FakeClock(), new MemoryLimits(), new RecordingLog());
        }

        GoToSurfaceTool GoTo() => new(_character, _world, _room, _settings);

        [Test]
        public void GoToSurface_WalksToTheNearestMatchAndFacesIt()
        {
            _world.AddBox("table", new Vector3(5, 0, 0), new Vector3(1f, 0.75f, 1f));
            _world.AddBox("table", new Vector3(0, 0, 2), new Vector3(1.2f, 0.75f, 0.8f));

            var result = ToolHarness.Run(GoTo(), "{\"label\":\"desk\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"walking\",\"label\":\"table\",\"eta_s\":4}"));
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(0, 0, 1.35f)), Is.LessThan(1e-4f));
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(0, 0.75f, 2)));
            Assert.That(result.EndsTurn, Is.False);
        }

        [Test]
        public void GoToSurface_RoomNotScanned_FailsSoTheModelCanOfferAScan()
        {
            _room.Status = RoomScanStatus.Unscanned;
            _world.AddBox("couch", new Vector3(0, 0, 2), Vector3.one);

            var result = ToolHarness.Run(GoTo(), "{\"label\":\"couch\"}");

            Assert.That((string)ToolHarness.Output(result)["error"], Is.EqualTo("room_not_scanned"));
            Assert.That(_character.MoveTarget, Is.Null);
        }

        [Test]
        public void GoToSurface_NoSuchFurniture_FailsWithTheLabel()
        {
            _world.AddBox("couch", new Vector3(0, 0, 2), Vector3.one);

            var result = ToolHarness.Run(GoTo(), "{\"label\":\"bed\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"no_surface\",\"detail\":\"bed\"}"));
        }

        [Test]
        public void GoToSurface_Unreachable_StillLooksAtIt()
        {
            _world.AddBox("bed", new Vector3(3, 0, 0), new Vector3(2f, 0.5f, 1.6f));
            _character.Reachable = false;

            var result = ToolHarness.Run(GoTo(), "{\"label\":\"bed\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"unreachable\",\"label\":\"bed\"}"));
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(3, 0.5f, 0)));
        }

        [TestCase("{\"label\":\"kitchen\"}")]
        [TestCase("{\"label\":\"sofa\"}")]
        [TestCase("{}")]
        [TestCase("{\"label\":\"couch\",\"perch\":true}")]
        public void GoToSurface_ArgumentsOutsideTheSchema_AreRejectedBeforeExecution(string json)
        {
            var registry = new ToolRegistry();
            registry.Register(GoTo());

            Assert.That(registry.TryValidate(ToolNames.GoToSurface, json, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void GoToSurface_IsLocomotionAndWithheldWhenSeated()
        {
            var registry = new ToolRegistry();
            registry.Register(GoTo());
            var seated = new FakeUserPreferences();
            seated.Current.Seated = true;

            Assert.That(GoTo().Definition.Concurrency, Is.EqualTo(ToolConcurrency.Locomotion));
            Assert.That(registry.DefinitionsFor(FakePersonaProvider.Profile("companion"), seated), Is.Empty);
        }

        [Test]
        public void RequestRoomScan_QueuesTheScanSpeaksAndEndsTheTurn()
        {
            var result = ToolHarness.Run(new RequestRoomScanTool(_room, _localizer));

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"opening\"}"));
            Assert.That(result.EndsTurn, Is.True);
            Assert.That(result.SpokenText, Is.EqualTo(CoreLines.Get(_localizer, CoreLines.ScanOpening)));
            Assert.That(result.SpokenText, Is.Not.Empty);
            Assert.That(_room.ScanRequests, Is.EqualTo(1));
        }

        [Test]
        public void RequestRoomScan_WithoutSpaceSetup_FailsAndQueuesNothing()
        {
            _room.CanRequestScan = false;

            var result = ToolHarness.Run(new RequestRoomScanTool(_room, _localizer));

            Assert.That((string)ToolHarness.Output(result)["error"], Is.EqualTo("scan_unavailable"));
            Assert.That(result.SpokenText, Is.Null);
            Assert.That(_room.ScanRequests, Is.EqualTo(0));
        }

        [Test]
        public void RequestRoomScan_RejectsArguments()
        {
            var registry = new ToolRegistry();
            registry.Register(new RequestRoomScanTool(_room, _localizer));

            Assert.That(registry.TryValidate(ToolNames.RequestRoomScan, "{\"now\":true}", out _, out _), Is.False);
        }

        [Test]
        public void LookAt_FurnitureWord_LooksAtTheTopOfTheNearestOne()
        {
            _world.AddBox("couch", new Vector3(0, 0, 2), new Vector3(2f, 0.8f, 1f));
            var tool = new LookAtTool(_character, new FakeUserPoseProvider(), _world, _scene, _settings);

            var result = ToolHarness.Run(tool, "{\"target\":\"sofa\"}");

            Assert.That(result.Ok, Is.True);
            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(0, 0.8f, 2)));
        }

        [Test]
        public void LookAt_SeenObjectLabel_LooksAtIt()
        {
            _scene.AddObject("tv remote", new Vector3(1, 0.5f, 1), "s");
            var tool = new LookAtTool(_character, new FakeUserPoseProvider(), _world, _scene, _settings);

            ToolHarness.Run(tool, "{\"target\":\"remote\"}");

            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(1, 0.5f, 1)));
        }
    }
}
