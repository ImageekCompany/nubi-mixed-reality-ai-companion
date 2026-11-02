using System;
using System.Linq;
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
    public sealed class CharacterToolTests
    {
        RecordingCharacterController _character;
        FakeWorldQuery _world;
        FakeUserPoseProvider _user;
        SceneMemory _scene;
        FakeSpatialAnchors _anchors;
        FakeClock _clock;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _character = new RecordingCharacterController { Position = new Vector3(0, 0.05f, 0) };
            _world = new FakeWorldQuery();
            _user = new FakeUserPoseProvider();
            _anchors = new FakeSpatialAnchors();
            _clock = new FakeClock();
            _log = new RecordingLog();
            _scene = new SceneMemory("unused", _anchors, _clock, new MemoryLimits(), _log);
        }

        LookAtTool LookAt() => new(_character, _user, _world, _scene, new LocomotionSettings());

        [Test]
        public void LookAt_DescriptionSendsObjectSearchesToFindObject()
        {
            // "Nubi, where is my mug?" with the mug in scene memory called look_at on device (selftest-report.md, f311081).
            Assert.That(LookAt().Definition.Description,
                        Does.Contain("never for finding or locating an object the user is looking for; use find_object for that"));
        }

        [Test]
        public void LookAt_User()
        {
            var result = ToolHarness.Run(LookAt(), "{\"target\":\"user\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"looking\",\"target\":\"user\"}"));
            Assert.That(_character.LastLook?.Kind, Is.EqualTo(LookTargetKind.User));
        }

        [Test]
        public void LookAt_Pointed_UsesTheRaycastHit()
        {
            _user.PointingRay = new Ray(new Vector3(0, 1, 0), Vector3.forward);
            _world.WallZ = 3;

            ToolHarness.Run(LookAt(), "{\"target\":\"pointed\"}");

            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(0, 1, 3)));
        }

        [Test]
        public void LookAt_PointedWithoutPointing_Fails()
        {
            var result = ToolHarness.Run(LookAt(), "{\"target\":\"pointed\"}");

            Assert.That((string)ToolHarness.Output(result)["error"], Is.EqualTo("no_pointing"));
        }

        [Test]
        public void LookAt_SeenObjectOrUnknown()
        {
            var mug = _scene.AddObject("mug", new Vector3(1, 1, 1), "s");

            ToolHarness.Run(LookAt(), "{\"target\":\"" + mug.Id + "\"}");
            var unknown = ToolHarness.Run(LookAt(), "{\"target\":\"obj_99\"}");

            Assert.That(_character.LastLook?.Point, Is.EqualTo(new Vector3(1, 1, 1)));
            Assert.That((string)ToolHarness.Output(unknown)["error"], Is.EqualTo("unknown_object"));
        }

        [Test]
        public void Stop_HaltsAndEndsTheTurn()
        {
            var result = ToolHarness.Run(new StopTool(_character));

            Assert.That(_character.StopCount, Is.EqualTo(1));
            Assert.That(result.EndsTurn, Is.True);
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"stopped\"}"));
        }

        [Test]
        public void RememberPlace_SavesTheUsersFloorPositionWithAnAnchor()
        {
            _user.Head = new Pose(new Vector3(2, 1.6f, -1), Quaternion.Euler(0, 90, 0));
            var tool = new RememberPlaceTool(_scene, _anchors, _user, _character, _clock, _log);

            var result = ToolHarness.Run(tool, "{\"name\":\" Sofa \"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"saved\",\"name\":\"sofa\",\"anchored\":true}"));
            Assert.That(_scene.TryGetPlace("sofa", out var place), Is.True);
            Assert.That(place.Pose.position, Is.EqualTo(new Vector3(2, 0.05f, -1)));
            Assert.That(place.AnchorId, Is.EqualTo("anchor-1"));
        }

        [Test]
        public void RememberPlace_WithoutAnchorPersistence_StillRemembersThePose()
        {
            _anchors.Supported = false;
            var tool = new RememberPlaceTool(_scene, _anchors, _user, _character, _clock, _log);

            var result = ToolHarness.Run(tool, "{\"name\":\"desk\"}");

            Assert.That((bool)ToolHarness.Output(result)["anchored"], Is.False);
            Assert.That(_scene.TryGetPlace("desk", out _), Is.True);
        }

        [Test]
        public void RememberPlace_AnchorSaveSlowerThanItsTimeout_RemembersThePlaceUnanchoredInsideTheToolTimeout()
        {
            _anchors.Hang = true;
            var tool = new RememberPlaceTool(_scene, _anchors, _user, _character, _clock, _log);
            var run = ToolHarness.Start(tool, "{\"name\":\"desk\"}").Preserve();

            _clock.Advance(TimeSpan.FromSeconds(3));

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That((bool)ToolHarness.Output(run.GetAwaiter().GetResult())["anchored"], Is.False);
            Assert.That(_scene.TryGetPlace("desk", out var place), Is.True);
            Assert.That(place.AnchorId, Is.Null);
            Assert.That(tool.Definition.Timeout, Is.GreaterThan(TimeSpan.FromSeconds(3)));
        }

        [Test]
        public void RememberPlace_TurnCancelledDuringTheAnchorSave_RemembersNothing()
        {
            _anchors.Hang = true;
            using var cts = new CancellationTokenSource();
            var tool = new RememberPlaceTool(_scene, _anchors, _user, _character, _clock, _log);
            var run = ToolHarness.Start(tool, "{\"name\":\"desk\"}", ct: cts.Token).Preserve();

            cts.Cancel();

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_scene.TryGetPlace("desk", out _), Is.False);
        }

        [Test]
        public void SetPersona_OffersEveryPersonaButPsychologistAndOnlyRequests()
        {
            var personas = new FakePersonaProvider(FakePersonaProvider.Profile("companion"),
                FakePersonaProvider.Profile("guru"), FakePersonaProvider.Profile("psychologist"));
            var tool = new SetPersonaTool(personas);

            var result = ToolHarness.Run(tool, "{\"id\":\"guru\"}");

            Assert.That(tool.Definition.ParametersJsonSchema, Does.Contain("[\"companion\",\"guru\"]"));
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"switching\",\"id\":\"guru\"}"));
            Assert.That(result.EndsTurn, Is.True);
            Assert.That(personas.PendingSwitchId, Is.EqualTo("guru"));
            Assert.That(personas.Current.Id, Is.EqualTo("companion"));
            Assert.That(personas.ApplyCount, Is.Zero);
        }

        [Test]
        public void SetPersona_Psychologist_IsRejectedEvenIfTheSchemaWereBypassed()
        {
            var personas = new FakePersonaProvider(FakePersonaProvider.Profile("companion"),
                FakePersonaProvider.Profile("psychologist"));
            var tool = new SetPersonaTool(personas);

            var result = Sync.Run(tool.Execute(new ToolArguments(new System.Collections.Generic.Dictionary<string, object>
                { ["id"] = "psychologist" }), ToolHarness.Context(), CancellationToken.None));

            Assert.That(result.Ok, Is.False);
            Assert.That(personas.PendingSwitchId, Is.Null);
        }

        [Test]
        public void Timeouts_MatchTheCatalogue()
        {
            Assert.That(LookAt().Definition.Timeout, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(new RememberPlaceTool(_scene, _anchors, _user, _character, _clock, _log).Definition.Timeout,
                Is.EqualTo(TimeSpan.FromSeconds(5)));
        }
    }
}
