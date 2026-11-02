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
using P = Jolly.Character.NubiAnimatorParameters;

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

        static readonly Vector3 WalkTarget = new(1.5f, 0f, -1.5f);

        [UnityTest]
        public IEnumerator Jump_AfterAWalkStarted_LetsItArriveThenJumpsStandingStill() =>
            UniTask.ToCoroutine(() => ArrivesThenPlays(ToolNames.Actions.Jump, "Jump"));

        [UnityTest]
        public IEnumerator Dance_AfterAWalkStarted_LetsItArriveThenDancesStandingStill() =>
            UniTask.ToCoroutine(() => ArrivesThenPlays(ToolNames.Actions.Dance, "Dance"));

        /// <summary>[come_here, jump] in one round: the walk the model was told about arrives, then the move plays.</summary>
        async UniTask ArrivesThenPlays(string name, string state)
        {
            // Arrange
            var layer = _rig.Animator.GetLayerIndex(P.ActionLayer);
            Assert.That(_rig.Nubi.TryStartMoveTo(WalkTarget, 0.1f, out _), Is.True);
            var walk = _rig.Nubi.WhenArrived(CancellationToken.None);
            await CharacterTestRig.Until(() => _rig.Nubi.IsMoving, 1f);

            // Act
            var action = Run(name);
            var movedWhilePlaying = 0f;
            var played = false;
            var from = Vector3.zero;
            while (action.Status == UniTaskStatus.Pending)
            {
                var playing = _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(state);
                if (playing && !played)
                    from = _rig.Nubi.Position;
                played |= playing;
                if (playing)
                    movedWhilePlaying = Mathf.Max(movedWhilePlaying, RelativeTarget.FlatDistance(_rig.Nubi.Position, from));
                await UniTask.Yield();
            }

            // Assert
            Assert.That((await action).OutputJson, Is.EqualTo("{\"status\":\"done\"}"));
            Assert.That(await walk, Is.EqualTo(MoveOutcome.Arrived), "the walk was not cut short");
            Assert.That(played, Is.True);
            Assert.That(movedWhilePlaying, Is.LessThan(0.05f), "never played while sliding");
        }

        [UnityTest]
        public IEnumerator Dance_WalkStartsDuringIt_EndsTheDanceAndSaysSo() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: [dance, come_here] in one round, or a tap to move during a dance.
            var layer = _rig.Animator.GetLayerIndex(P.ActionLayer);
            var dance = Run(ToolNames.Actions.Dance, "{\"duration_s\":8}");
            Assert.That(await CharacterTestRig.Until(
                () => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName("Dance"), 1f), Is.True);

            // Act
            Assert.That(_rig.Nubi.TryStartMoveTo(WalkTarget, 0.1f, out _), Is.True);
            var result = await dance;
            var left = await CharacterTestRig.Until(
                () => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), 0.5f);

            // Assert
            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"interrupted_by_walk\"}"));
            Assert.That(left, Is.True, "the dance faded out as the walk began");
            Assert.That(await _rig.Nubi.WhenArrived(CancellationToken.None), Is.EqualTo(MoveOutcome.Arrived));
        });

        [UnityTest]
        public IEnumerator Spin_WhileFollowing_EndsFollowingAndSpinsOnTheSpot() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: the user is 2 m away, beyond the follow range, so following walks.
            _rig.Nubi.Follow(1f, 1.5f);
            await CharacterTestRig.Until(() => _rig.Nubi.IsMoving, 2f);

            // Act
            var action = Run(ToolNames.Actions.Spin);
            var from = _rig.Nubi.Position;
            var drift = 0f;
            while (action.Status == UniTaskStatus.Pending)
            {
                drift = Mathf.Max(drift, RelativeTarget.FlatDistance(_rig.Nubi.Position, from));
                await UniTask.Yield();
            }

            // Assert
            Assert.That((await action).OutputJson, Is.EqualTo("{\"status\":\"done\"}"));
            Assert.That(_rig.Nubi.Mode, Is.EqualTo(LocomotionMode.None));
            Assert.That(drift, Is.LessThan(0.1f));
        });
    }
}
#endif
