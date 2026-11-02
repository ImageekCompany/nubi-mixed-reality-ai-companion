#if UNITY_EDITOR
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Tools;
using Jolly.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// actions.move_relative on the real Nubi and the production NavMesh bake (ADR-0047). The user stands 2 m in front of
    /// Nubi at z = 2, facing it (-z), so "behind me" is at z = 3 and "my left" is at x = +1.
    /// </summary>
    public sealed class MoveRelativeTests
    {
        CharacterTestRig _rig;

        [SetUp]
        public void SetUp() => _rig = CharacterTestRig.Create();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        async UniTask<ToolResult> Run(string argsJson)
        {
            var tool = new MoveRelativeTool(_rig.Nubi, _rig.User, new RigWorld(_rig.NavMesh), new LocomotionSettings(), new RecordingLog());
            var registry = new ToolRegistry();
            registry.RegisterNamespace(new ToolNamespace(ToolNames.Actions.Namespace, "d"));
            registry.Register(tool);
            Assert.That(registry.TryValidate(tool.Definition.Name, argsJson, out var args, out var error), Is.True, error);
            return await tool.Execute(args, new ToolContext(), CancellationToken.None);
        }

        static readonly Vector3 Behind = new(0f, 0f, 3f);

        [UnityTest]
        public IEnumerator MoveRelative_BehindFromInFrontOfTheUser_ArrivesBehindThemWithoutPassingThrough() =>
            UniTask.ToCoroutine(async () =>
        {
            // Arrange: Nubi starts 2 m in front of the user, on the straight line to "behind me".
            var user = _rig.User.Head.position;
            var nearest = float.PositiveInfinity;

            // Act: one motion round the user; its arrival is the target's, not the turn's.
            var result = await Run("{\"direction\":\"behind\"}");
            var arrival = _rig.Nubi.WhenArrived(CancellationToken.None);
            var settled = await CharacterTestRig.Until(() =>
            {
                nearest = Mathf.Min(nearest, FlatDistance(_rig.Nubi.Position, user));
                return arrival.Status != UniTaskStatus.Pending;
            }, 20f);
            var arrivedAt = _rig.Nubi.Position;
            await CharacterTestRig.Until(() => _rig.Nubi.TryMeasureUser(out var body, out _) && body < 10f, 3f);

            // Assert
            Assert.That(result.OutputJson, Does.StartWith("{\"direction\":\"behind\",\"status\":\"walking\""));
            Assert.That(settled, Is.True);
            Assert.That(await arrival, Is.EqualTo(MoveOutcome.Arrived));
            Assert.That(FlatDistance(arrivedAt, Behind), Is.LessThan(0.3f), "arrived behind the user, not at the turn");
            Assert.That(nearest, Is.GreaterThanOrEqualTo(new LocomotionSettings().UserMinDistance),
                        $"kept clear of the user all the way round (nearest {nearest:0.00} m)");
            Assert.That(_rig.Nubi.TryMeasureUser(out var bodyOff, out _), Is.True);
            Assert.That(bodyOff, Is.LessThan(25f), "turned round to face the user");
        });

        [UnityTest]
        public IEnumerator MoveVia_UserStepsIntoTheSecondLeg_StopsAtTheTurn() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: round the user's -x side to behind them.
            var turn = new Vector3(-1f, 0f, 2f);
            Assert.That(_rig.Nubi.TryStartMoveVia(turn, Behind, 0.15f, 0.5f, out _), Is.True);
            var arrival = _rig.Nubi.WhenArrived(CancellationToken.None);
            await CharacterTestRig.Until(() => FlatDistance(_rig.Nubi.Position, Vector3.zero) > 0.3f, 5f);

            // Act: the user steps onto the line from the turn to behind them.
            _rig.User.Head = new Pose(new Vector3(-0.5f, 1.6f, 2.5f), Quaternion.Euler(0f, 180f, 0f));
            var settled = await CharacterTestRig.Until(() => arrival.Status != UniTaskStatus.Pending, 20f);

            // Assert
            Assert.That(settled, Is.True);
            Assert.That(await arrival, Is.EqualTo(MoveOutcome.Unreachable));
            Assert.That(FlatDistance(_rig.Nubi.Position, turn), Is.LessThan(0.4f));
            Assert.That(_rig.Nubi.IsMoving, Is.False);
        });

        [UnityTest]
        public IEnumerator MoveRelative_Left_ArrivesOnTheUsersLeft() => UniTask.ToCoroutine(async () =>
        {
            // Act
            await Run("{\"direction\":\"left\",\"distance_m\":1.5}");
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None);

            // Assert
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
            var position = _rig.Nubi.Position;
            Assert.That(Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(1.5f, 0f, 2f)), Is.LessThan(0.3f));
        });

        [UnityTest]
        public IEnumerator MoveRelative_BehindAUserAtTheFloorsEdge_ReportsUnreachable() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: the 10 m floor ends at z = 5; 3 m behind a user at z = 4.5 is far off it
            _rig.User.Head = new Pose(new Vector3(0f, 1.6f, 4.5f), Quaternion.Euler(0f, 180f, 0f));

            // Act
            var result = await Run("{\"direction\":\"behind\",\"distance_m\":3}");

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"direction\":\"behind\",\"status\":\"unreachable\"}"));
            Assert.That(_rig.Nubi.IsMoving, Is.False);
        });

        static float FlatDistance(Vector3 a, Vector3 b) => RelativeTarget.FlatDistance(a, b);
    }
}
#endif
