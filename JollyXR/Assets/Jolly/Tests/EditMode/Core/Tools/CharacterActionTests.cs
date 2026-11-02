using System.Linq;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Tools
{
    /// <summary>The character action catalog and its tools (ADR-0047).</summary>
    public sealed class CharacterActionTests
    {
        RecordingCharacterController _character;
        FakeWorldQuery _world;
        FakeUserPoseProvider _user;
        RecordingLog _log;
        readonly LocomotionSettings _settings = new();

        [SetUp]
        public void SetUp()
        {
            _character = new RecordingCharacterController { Position = new Vector3(0f, 0f, 1.5f) };
            _world = new FakeWorldQuery();
            // The user stands at the origin looking along +z, so their right is +x.
            _user = new FakeUserPoseProvider { Head = new Pose(new Vector3(0f, 1.6f, 0f), Quaternion.identity) };
            _log = new RecordingLog();
        }

        CharacterActionCatalog Catalog() => CharacterActionCatalog.For(_character, _user, _world, _settings, _log);

        MoveRelativeTool MoveRelative() => new(_character, _user, _world, _settings, _log);

        [Test]
        public void For_CharacterWithEveryClip_OffersEveryAction()
        {
            // Act
            var names = Catalog().Tools.Select(t => t.Definition.Name);

            // Assert
            Assert.That(names, Is.EqualTo(new[]
            {
                ToolNames.Actions.Jump, ToolNames.Actions.Dance, ToolNames.Actions.Bow, ToolNames.Actions.Spin,
                ToolNames.Actions.Celebrate, ToolNames.Actions.MoveRelative, ToolNames.Actions.Wave, ToolNames.Actions.Nod,
                ToolNames.Actions.ShakeHead, ToolNames.Actions.Shrug, ToolNames.Actions.Think, ToolNames.Actions.Bounce,
                ToolNames.Actions.Stretch, ToolNames.Actions.LookAround,
            }));
        }

        [Test]
        public void For_CharacterWithoutDanceClip_DoesNotOfferDance()
        {
            // Arrange
            _character.Missing.Add(GestureId.Dance);

            // Act
            var catalog = Catalog();

            // Assert
            Assert.That(catalog.Tools.Select(t => t.Definition.Name), Has.No.Member(ToolNames.Actions.Dance));
            Assert.That(catalog.Namespace.Description, Does.Not.Contain("dance"));
        }

        [Test]
        public void For_CharacterWithoutJumpClip_DropsJumpAndCelebrateButKeepsTheRest()
        {
            // Arrange
            _character.Missing.Add(GestureId.Jump);

            // Act
            var names = Catalog().Tools.Select(t => t.Definition.Name).ToArray();

            // Assert
            Assert.That(names, Has.No.Member(ToolNames.Actions.Jump).And.No.Member(ToolNames.Actions.Celebrate));
            Assert.That(names, Has.Member(ToolNames.Actions.Bow).And.Member(ToolNames.Actions.MoveRelative));
        }

        [Test]
        public void Namespace_Description_NamesNoActionSoItHoldsForEveryRequest()
        {
            // Act
            var ns = Catalog().Namespace;

            // Assert
            Assert.That(ns.Name, Is.EqualTo("actions"));
            Assert.That(ns.Description, Is.EqualTo(CharacterActionCatalog.Description));
            foreach (var tool in Catalog().Tools)
                Assert.That(ns.Description, Does.Not.Contain(tool.Definition.Name.Substring("actions.".Length).Replace('_', ' ')),
                            tool.Definition.Name);
            Assert.That(ns.Description, Does.Contain("never say you did it without calling it"));
            Assert.That(ns.Description, Does.Contain("surprise them"));
        }

        [Test]
        public void NamespacesOf_PersonaWithholdingTheMove_DescribesNoMove()
        {
            // Arrange
            var registry = new ToolRegistry();
            Catalog().RegisterInto(registry);
            var persona = new PersonaProfile { AllowedTools = new[] { ToolNames.Actions.Dance } };
            var tools = registry.DefinitionsFor(persona, null);

            // Act
            var ns = registry.NamespacesOf(tools).Single();

            // Assert
            Assert.That(tools.Select(t => t.Name), Has.No.Member(ToolNames.Actions.MoveRelative));
            Assert.That(ns.Description, Does.Not.Contain("move").And.Not.Contain("behind").And.Not.Contain("relative"));
        }

        [Test]
        public void For_PersonaListingOnlyGestures_OffersOnlyThose()
        {
            // Arrange
            var registry = new ToolRegistry();
            Catalog().RegisterInto(registry);
            var persona = new PersonaProfile { AllowedTools = new[] { ToolNames.Actions.Wave, ToolNames.Actions.Nod } };

            // Act
            var names = registry.DefinitionsFor(persona, new FakeUserPreferences()).Select(d => d.Name);

            // Assert
            Assert.That(names, Is.EqualTo(new[] { ToolNames.Actions.Wave, ToolNames.Actions.Nod }));
        }

        [TestCase(ToolNames.Actions.Wave, GestureId.Wave)]
        [TestCase(ToolNames.Actions.Nod, GestureId.Nod)]
        [TestCase(ToolNames.Actions.ShakeHead, GestureId.ShakeHead)]
        [TestCase(ToolNames.Actions.Shrug, GestureId.Shrug)]
        [TestCase(ToolNames.Actions.Think, GestureId.Think)]
        [TestCase(ToolNames.Actions.Bounce, GestureId.HappyBounce)]
        [TestCase(ToolNames.Actions.Stretch, GestureId.Stretch)]
        [TestCase(ToolNames.Actions.LookAround, GestureId.LookAround)]
        public void Gesture_PlaysItsGestureAlongsideAWalk(string name, GestureId gesture)
        {
            // Arrange
            var tool = Catalog().Tools.Single(t => t.Definition.Name == name);
            _character.TryStartMoveTo(Vector3.one, 0.1f, out _);

            // Act
            var result = Run(tool, "{}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"done\"}"));
            Assert.That(_character.Gestures, Is.EqualTo(new[] { gesture }));
            Assert.That(tool.Definition.Concurrency, Is.EqualTo(ToolConcurrency.Parallel), "a wave while talking or walking");
            Assert.That(_character.StopCount, Is.Zero, "an upper-body gesture never stops a walk");
        }

        [TestCase(ToolNames.Actions.Jump)]
        [TestCase(ToolNames.Actions.Bow)]
        [TestCase(ToolNames.Actions.Celebrate)]
        [TestCase(ToolNames.Actions.Dance)]
        [TestCase(ToolNames.Actions.Spin)]
        public void WholeBodyAction_DuringAWalk_LetsItArriveThenPlays(string name)
        {
            // Arrange: the round's earlier come_here or move_relative is still walking.
            var tool = Catalog().Tools.Single(t => t.Definition.Name == name);
            _character.TryStartMoveTo(Vector3.one, 0.1f, out _);

            // Act
            var result = Run(tool, "{}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"done\"}"));
            Assert.That(_character.StopCount, Is.Zero, "the walk the model was told about is not cut short");
            Assert.That(_character.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(tool.Definition.Concurrency, Is.EqualTo(ToolConcurrency.Exclusive));
        }

        [TestCase(ToolNames.Actions.Jump)]
        [TestCase(ToolNames.Actions.Dance)]
        [TestCase(ToolNames.Actions.Spin)]
        public void WholeBodyAction_WhileFollowing_EndsFollowing(string name)
        {
            // Arrange
            _character.Follow(1f, 1.5f);

            // Act
            var result = Run(Catalog().Tools.Single(t => t.Definition.Name == name), "{}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"done\"}"));
            Assert.That(_character.StopCount, Is.EqualTo(1));
            Assert.That(_character.Mode, Is.EqualTo(LocomotionMode.None));
        }

        [TestCase(ToolNames.Actions.Jump)]
        [TestCase(ToolNames.Actions.Celebrate)]
        [TestCase(ToolNames.Actions.Dance)]
        [TestCase(ToolNames.Actions.Spin)]
        public void WholeBodyAction_WalkStartsDuringIt_ReportsInterruptedByWalk(string name)
        {
            // Arrange: a later come_here, a tap or follow starts walking mid-move, which ends the move.
            var tool = Catalog().Tools.Single(t => t.Definition.Name == name);
            _character.WhilePlaying = () => _character.TryStartMoveTo(Vector3.one, 0.1f, out _);

            // Act
            var result = Run(tool, "{}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"interrupted_by_walk\"}"));
            Assert.That(_character.Gestures.Count + _character.Looped.Count, Is.LessThanOrEqualTo(name == ToolNames.Actions.Spin ? 2 : 1),
                        "no further gesture after the walk started");
            Assert.That(_log.Entries.Select(e => e.Message), Has.Some.Contains("status=interrupted_by_walk"));
        }

        [TestCase(ToolNames.Actions.Jump)]
        [TestCase(ToolNames.Actions.Dance)]
        [TestCase(ToolNames.Actions.Spin)]
        public void WholeBodyAction_Timeout_CoversAWalkToFinishFirst(string name)
        {
            // Act
            var timeout = Catalog().Tools.Single(t => t.Definition.Name == name).Definition.Timeout;

            // Assert
            Assert.That(timeout.TotalSeconds, Is.GreaterThanOrEqualTo(14));
        }

        [Test]
        public void WholeBodyAction_StandingStill_StopsNothing()
        {
            // Act
            Run(Catalog().Tools.Single(t => t.Definition.Name == ToolNames.Actions.Jump), "{}");

            // Assert
            Assert.That(_character.StopCount, Is.Zero);
        }

        [Test]
        public void RegisterInto_RegistersTheNamespaceAndEveryActionForTheRequest()
        {
            // Arrange
            var registry = new ToolRegistry();

            // Act
            Catalog().RegisterInto(registry);

            // Assert
            var tools = registry.DefinitionsFor(null, new FakeUserPreferences());
            Assert.That(registry.NamespacesOf(tools).Select(n => n.Name), Is.EqualTo(new[] { "actions" }));
            Assert.That(registry.TryGet(ToolNames.Actions.Dance, out _), Is.True);
            Assert.That(registry.TryValidate(ToolNames.Actions.MoveRelative, "{\"direction\":\"behind\"}", out _, out var error),
                        Is.True, error);
        }

        [Test]
        public void DefinitionsFor_Seated_StillOffersMoveRelative()
        {
            // Arrange
            var registry = new ToolRegistry();
            Catalog().RegisterInto(registry);
            var preferences = new FakeUserPreferences();
            preferences.Current.Seated = true;

            // Act
            var names = registry.DefinitionsFor(null, preferences).Select(d => d.Name).ToArray();

            // Assert: it moves the character around the user, who stays put (ADR-0047).
            Assert.That(names, Has.Member(ToolNames.Actions.MoveRelative));
            Assert.That(names, Has.Member(ToolNames.Actions.Dance));
        }

        [TestCase("front", 0f, 1f)]
        [TestCase("behind", 0f, -1f)]
        [TestCase("left", -1f, 0f)]
        [TestCase("right", 1f, 0f)]
        public void MoveRelative_Direction_WalksToThatSideOfTheUser(string direction, float x, float z)
        {
            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"" + direction + "\"}");

            // Assert
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(x, 0f, z)), Is.LessThan(1e-4f));
            Assert.That(_character.LastLook?.Kind, Is.EqualTo(LookTargetKind.User), "ends facing the user");
        }

        [Test]
        public void MoveRelative_BehindFromInFrontOfTheUser_WalksRoundThemInOneMotionOnTheWalksRight()
        {
            // Arrange: the character stands 1.5 m in front of the user (origin, facing +z).

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"behind\"}");

            // Assert: walking -z, the walk's right is -x; the turn is DetourRadius from the user.
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(_character.MoveTargets, Has.Count.EqualTo(1), "one motion, so WhenArrived means the target");
            Assert.That(Vector3.Distance(_character.Waypoint.Value, new Vector3(-_settings.DetourRadius, 0f, 0f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(0f, 0f, -1f)), Is.LessThan(1e-4f));
            Assert.That(_character.MinUserDistance, Is.EqualTo(_settings.UserMinDistance), "the second leg is checked again");
            Assert.That(RelativeTarget.PassDistance(new Vector3(0f, 0f, 1.5f), _character.Waypoint.Value, Vector3.zero),
                        Is.GreaterThanOrEqualTo(_settings.UserPassClearance));
            Assert.That(RelativeTarget.PassDistance(_character.Waypoint.Value, _character.MoveTarget.Value, Vector3.zero),
                        Is.GreaterThanOrEqualTo(_settings.UserPassClearance));
        }

        [Test]
        public void MoveRelative_TurnSnapsInsideTheClearance_GoesRoundTheOtherSide()
        {
            // Arrange: the walkable floor on the user's -x side ends 0.55 m from them, so that turn snaps in.
            _world.Snap = p => p.x < 0f ? new Vector3(-0.55f, p.y, p.z) : p;

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"behind\"}");

            // Assert
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(_character.Waypoint.Value.x, Is.EqualTo(_settings.DetourRadius).Within(1e-4f));
        }

        [Test]
        public void MoveRelative_EveryTurnInsideTheClearance_ReportsUnreachable()
        {
            // Arrange: both turns snap to 0.55 m from the user, past the 0.5 m minimum but inside the 0.6 m clearance.
            _world.Snap = p => Mathf.Abs(p.x) > 0.7f ? new Vector3(Mathf.Sign(p.x) * 0.55f, p.y, p.z) : p;

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"behind\"}");

            // Assert
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("unreachable"));
            Assert.That(_character.MoveTarget, Is.Null);
        }

        [Test]
        public void MoveRelative_StartingNearerThanTheClearance_StillGoesRound()
        {
            // Arrange: 0.55 m in front of the user, already inside the 0.6 m clearance.
            _character.Position = new Vector3(0f, 0f, 0.55f);

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"behind\"}");

            // Assert
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("walking"));
            Assert.That(_character.Waypoint, Is.Not.Null);
        }

        [Test]
        public void MoveRelative_CharacterOffToOneSide_TurnsOnThatSide()
        {
            // Arrange: in front of the user and a little to their right (+x).
            _character.Position = new Vector3(0.3f, 0f, 1.5f);

            // Act
            Run(MoveRelative(), "{\"direction\":\"behind\"}");

            // Assert
            Assert.That(_character.Waypoint.Value.x, Is.GreaterThan(0.5f));
        }

        [Test]
        public void MoveRelative_LineClearOfTheUser_WalksStraightThere()
        {
            // Act
            Run(MoveRelative(), "{\"direction\":\"left\"}");

            // Assert
            Assert.That(_character.MoveTargets, Has.Count.EqualTo(1));
            Assert.That(_character.Waypoint, Is.Null);
        }

        [Test]
        public void MoveRelative_UserTurned_FollowsTheirYawNotPitch()
        {
            // Arrange: facing +x and looking down at the floor
            _user.Head = new Pose(new Vector3(1f, 1.6f, 1f), Quaternion.Euler(50f, 90f, 0f));

            // Act
            Run(MoveRelative(), "{\"direction\":\"behind\",\"distance_m\":2}");

            // Assert
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(-1f, 0f, 1f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void MoveRelative_Away_BacksOffAlongTheUserToCharacterLine()
        {
            // Act
            Run(MoveRelative(), "{\"direction\":\"away\",\"distance_m\":0.5}");

            // Assert
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(0f, 0f, 2f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void MoveRelative_Closer_StopsAtTheCloserDistanceFromTheUser()
        {
            // Arrange
            _character.Position = new Vector3(3f, 0f, 0f);

            // Act
            Run(MoveRelative(), "{\"direction\":\"closer\"}");

            // Assert
            Assert.That(Vector3.Distance(_character.MoveTarget.Value, new Vector3(_settings.CloserDistance, 0f, 0f)),
                        Is.LessThan(1e-4f));
        }

        [Test]
        public void MoveRelative_CloserWhenAlreadyClose_StaysAndSaysSo()
        {
            // Arrange
            _character.Position = new Vector3(0f, 0f, 0.75f);

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"closer\"}");

            // Assert
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("already_close"));
            Assert.That(_character.MoveTarget, Is.Null);
        }

        [Test]
        public void MoveRelative_NoWalkableFloor_ReportsUnreachable()
        {
            // Arrange
            _world.Walkable = false;

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"behind\"}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"direction\":\"behind\",\"status\":\"unreachable\"}"));
            Assert.That(_character.MoveTarget, Is.Null);
            Assert.That(_log.Entries.Select(e => e.Message), Has.Some.Contains("evt=action.move_relative dir=behind dist_m=1.00 status=unreachable"));
        }

        [Test]
        public void MoveRelative_PathBlocked_ReportsUnreachable()
        {
            // Arrange
            _character.Reachable = false;

            // Act
            var result = Run(MoveRelative(), "{\"direction\":\"left\"}");

            // Assert
            Assert.That((string)ToolHarness.Output(result)["status"], Is.EqualTo("unreachable"));
        }

        [Test]
        public void MoveRelative_DistanceOutOfRange_IsRejectedBySchema()
        {
            // Arrange
            var registry = new ToolRegistry();
            registry.RegisterNamespace(new ToolNamespace("actions", "d"));
            registry.Register(MoveRelative());

            // Act
            var ok = registry.TryValidate(ToolNames.Actions.MoveRelative, "{\"direction\":\"front\",\"distance_m\":0.2}",
                                          out _, out _);

            // Assert
            Assert.That(ok, Is.False);
        }

        [Test]
        public void Dance_Duration_LoopsTheDanceThatLong()
        {
            // Act
            var result = Run(new DanceTool(_character, _log), "{\"duration_s\":8}");

            // Assert
            Assert.That(result.Ok, Is.True);
            Assert.That(_character.Looped, Is.EqualTo(new[] { (GestureId.Dance, 8f) }));
            Assert.That(_log.Entries.Select(e => e.Message), Has.Some.Contains("evt=action.dance duration_s=8 status=done"));
        }

        [Test]
        public void Dance_NoDuration_DancesFourSeconds()
        {
            // Act
            Run(new DanceTool(_character, _log), "{}");

            // Assert
            Assert.That(_character.Looped.Single().seconds, Is.EqualTo(4f));
        }

        [Test]
        public void Celebrate_JumpsThenBounces()
        {
            // Arrange
            var celebrate = Catalog().Tools.Single(t => t.Definition.Name == ToolNames.Actions.Celebrate);

            // Act
            var result = Run(celebrate, "{}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"done\"}"));
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.Jump, GestureId.HappyBounce }));
            Assert.That(_log.Entries.Select(e => e.Message), Has.Some.Contains("evt=action.celebrate status=done"));
        }

        [TestCase("{}", 360f)]
        [TestCase("{\"turns\":2,\"direction\":\"left\"}", -720f)]
        [TestCase("{\"turns\":1.4,\"direction\":\"right\"}", 360f)]
        public void Spin_TurnsAndDirection_SpinThatFarWithAHappyBounce(string args, float degrees)
        {
            // Act
            Run(new SpinTool(_character, _log), args);

            // Assert
            Assert.That(_character.Spins, Is.EqualTo(new[] { degrees }));
            Assert.That(_character.Gestures, Is.EqualTo(new[] { GestureId.HappyBounce }));
        }

        /// <summary>Like <see cref="ToolHarness.Run"/>, which cannot register a tool whose name needs the actions namespace.</summary>
        static ToolResult Run(ITool tool, string argsJson)
        {
            var registry = new ToolRegistry();
            registry.RegisterNamespace(new ToolNamespace("actions", "d"));
            registry.Register(tool);
            Assert.That(registry.TryValidate(tool.Definition.Name, argsJson, out var args, out var error), Is.True, error);
            return Sync.Run(tool.Execute(args, ToolHarness.Context(), default));
        }
    }
}
