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
    /// <summary>The actions namespace's clip and spin actions on the real Nubi (ADR-0047).</summary>
    public sealed class BodyActionTests
    {
        CharacterTestRig _rig;

        [SetUp]
        public void SetUp() => _rig = CharacterTestRig.Create();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        UniTask<ToolResult> Run(string name, string argsJson = "{}")
        {
            var registry = new ToolRegistry();
            CharacterActionCatalog.For(_rig.Nubi, _rig.User, new RigWorld(_rig.NavMesh), new LocomotionSettings(), new RecordingLog())
                .RegisterInto(registry);
            Assert.That(registry.TryValidate(name, argsJson, out var args, out var error), Is.True, error);
            registry.TryGet(name, out var tool);
            return tool.Execute(args, new ToolContext(), CancellationToken.None);
        }

        [UnityTest]
        public IEnumerator Jump_DuringAWalk_StandsStillWhileItPlays() => UniTask.ToCoroutine(() => StandsStill(ToolNames.Actions.Jump));

        [UnityTest]
        public IEnumerator Spin_DuringAWalk_StandsStillWhileItPlays() => UniTask.ToCoroutine(() => StandsStill(ToolNames.Actions.Spin));

        async UniTask StandsStill(string name)
        {
            // Arrange: a walk across the floor, under way.
            Assert.That(_rig.Nubi.TryStartMoveTo(new Vector3(4f, 0f, -4f), 0.1f, out _), Is.True);
            await CharacterTestRig.Until(() => _rig.Nubi.IsMoving && _rig.Nubi.Position.sqrMagnitude > 0.04f, 3f);

            // Act
            var action = Run(name);
            var from = _rig.Nubi.Position;
            var drift = 0f;
            var movingDuring = false;
            while (action.Status == UniTaskStatus.Pending)
            {
                drift = Mathf.Max(drift, RelativeTarget.FlatDistance(_rig.Nubi.Position, from));
                movingDuring |= _rig.Nubi.IsMoving;
                await UniTask.Yield();
            }

            // Assert
            Assert.That((await action).Ok, Is.True);
            Assert.That(movingDuring, Is.False);
            Assert.That(drift, Is.LessThan(0.05f));
        }
    }
}
#endif
