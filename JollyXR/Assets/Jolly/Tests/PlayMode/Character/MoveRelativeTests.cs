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

        [UnityTest]
        public IEnumerator MoveRelative_Behind_ArrivesBehindTheUserAndFacesThem() => UniTask.ToCoroutine(async () =>
        {
            // Act
            var result = await Run("{\"direction\":\"behind\"}");
            var outcome = await _rig.Nubi.WhenArrived(CancellationToken.None);
            await CharacterTestRig.Until(() => _rig.Nubi.TryMeasureUser(out var body, out _) && body < 10f, 3f);

            // Assert
            Assert.That(result.OutputJson, Does.StartWith("{\"direction\":\"behind\",\"status\":\"walking\""));
            Assert.That(outcome, Is.EqualTo(MoveOutcome.Arrived));
            var position = _rig.Nubi.Position;
            Assert.That(Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(0f, 0f, 3f)), Is.LessThan(0.3f));
            Assert.That(_rig.Nubi.TryMeasureUser(out var bodyOff, out _), Is.True);
            Assert.That(bodyOff, Is.LessThan(25f), "turned round to face the user");
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
    }
}
#endif
