#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using P = Jolly.Character.NubiAnimatorParameters;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class GestureAndAnimatorTests
    {
        CharacterTestRig _rig;

        [SetUp]
        public void SetUp() => _rig = CharacterTestRig.Create();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [UnityTest]
        public IEnumerator PlayGesture_Wave_CompletesAndReturnsToEmpty() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var layer = _rig.Animator.GetLayerIndex(P.GestureLayer);
            var started = Time.time;

            // Act
            await _rig.Nubi.PlayGesture(GestureId.Wave, CancellationToken.None);

            // Assert
            Assert.That(Time.time - started, Is.InRange(1.2f, 3f), "Wave is a 2 s clip");
            Assert.That(_rig.Animator.GetInteger(P.GestureId), Is.EqualTo((int)GestureId.Wave));
            Assert.That(await CharacterTestRig.Until(
                () => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), 0.5f), Is.True);
        });

        [UnityTest]
        public IEnumerator PlayGesture_Cancelled_ThrowsAndFadesOut() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var layer = _rig.Animator.GetLayerIndex(P.GestureLayer);
            using var cts = new CancellationTokenSource();
            var gesture = _rig.Nubi.PlayGesture(GestureId.Stretch, cts.Token);
            await UniTask.DelayFrame(10);

            // Act
            cts.Cancel();

            // Assert
            var threw = false;
            try { await gesture; }
            catch (OperationCanceledException) { threw = true; }
            Assert.That(threw, Is.True);
            Assert.That(await CharacterTestRig.Until(
                () => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), 1f), Is.True);
        });

        [UnityTest]
        public IEnumerator PlayGesture_LookAround_SweepsAndCompletesOnTheClock() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var gesture = _rig.Nubi.PlayGesture(GestureId.LookAround, CancellationToken.None);

            // Act
            for (var step = 0; step < 3; step++)
            {
                await UniTask.DelayFrame(3);
                _rig.Clock.Advance(TimeSpan.FromSeconds(0.8));
            }

            // Assert
            Assert.That(await CharacterTestRig.Until(() => gesture.Status != UniTaskStatus.Pending, 1f), Is.True);
            Assert.That(gesture.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        });

        [UnityTest]
        public IEnumerator PlayGesture_NubiDestroyedMidClip_ThrowsCancelled() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var gesture = _rig.Nubi.PlayGesture(GestureId.Stretch, CancellationToken.None);
            await UniTask.DelayFrame(5);

            // Act
            Object.Destroy(_rig.Nubi.gameObject);

            // Assert
            Exception thrown = null;
            try { await gesture; }
            catch (Exception e) { thrown = e; }
            Assert.That(thrown, Is.InstanceOf<OperationCanceledException>());
        });

        [UnityTest]
        public IEnumerator PlayGesture_LookAroundOverlapping_OlderEndingKeepsTheNewerSweep() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: no Animator writes, so the head moves only with the look target
            _rig.Animator.enabled = false;
            var head = FindBone(_rig.Nubi.transform, "Head");
            await UniTask.Delay(TimeSpan.FromSeconds(_rig.Nubi.Settings.look.blendSeconds * 2f));
            var atUser = head.rotation;
            var hold = TimeSpan.FromSeconds(0.4);
            var older = _rig.Nubi.PlayGesture(GestureId.LookAround, CancellationToken.None);
            _rig.Clock.Advance(hold);
            var newer = _rig.Nubi.PlayGesture(GestureId.LookAround, CancellationToken.None);

            // Act: past the older sweep's end, halfway through the newer one's last step
            for (var i = 0; i < 5; i++)
                _rig.Clock.Advance(hold);
            await UniTask.DelayFrame(2);

            // Assert
            Assert.That(older.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(newer.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(Quaternion.Angle(head.rotation, atUser), Is.GreaterThan(5f), "still looking ahead, not at the user");
            _rig.Clock.Advance(hold);
            Assert.That(newer.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        });

        [UnityTest]
        public IEnumerator SetConversationState_SetsExactlyOneConversationBool() => UniTask.ToCoroutine(async () =>
        {
            // Act
            _rig.Nubi.SetConversationState(ConversationState.Speaking);
            await UniTask.DelayFrame(1);

            // Assert
            Assert.That(_rig.Animator.GetBool(P.Talking), Is.True);
            Assert.That(_rig.Animator.GetBool(P.Listening), Is.False);
            Assert.That(_rig.Animator.GetBool(P.Thinking), Is.False);

            _rig.Nubi.SetConversationState(ConversationState.Listening);
            Assert.That(_rig.Animator.GetBool(P.Listening), Is.True);
            Assert.That(_rig.Animator.GetBool(P.Talking), Is.False);
        });

        [UnityTest]
        public IEnumerator SetMood_SetsMoodParameter() => UniTask.ToCoroutine(async () =>
        {
            // Act
            _rig.Nubi.SetMood(Mood.Concerned);
            await UniTask.DelayFrame(1);

            // Assert
            Assert.That(_rig.Animator.GetInteger(P.Mood), Is.EqualTo((int)Mood.Concerned));
        });

        [UnityTest]
        public IEnumerator Walking_SetsSpeedAndClampedPlaybackRate() => UniTask.ToCoroutine(async () =>
        {
            // Act
            _rig.Nubi.TryStartMoveTo(new Vector3(0f, 0f, -3f), 0.15f, out _);
            var walking = await CharacterTestRig.Until(() => _rig.Animator.GetFloat(P.Speed) > 0.4f, 3f);

            // Assert
            Assert.That(walking, Is.True);
            Assert.That(_rig.Animator.GetFloat(P.LocomotionRate), Is.InRange(0.7f, 1.5f));
            Assert.That(await CharacterTestRig.Until(() => Vector3.Angle(_rig.Nubi.Forward, Vector3.back) < 20f, 1.5f),
                        Is.True, "body turns toward the walk");
        });

        static Transform FindBone(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>().Single(t => t.name == name);

        [Test]
        public void Controller_DeclaresEveryParameterTheRuntimeSets()
        {
            var controller = (AnimatorController)_rig.Animator.runtimeAnimatorController;
            var names = controller.parameters.Select(p => p.name).ToArray();

            Assert.That(names, Is.EquivalentTo(new[]
            {
                P.SpeedName, P.LocomotionRateName, P.ListeningName, P.ThinkingName, P.TalkingName,
                P.MoodName, P.GestureIdName, P.GestureName,
            }));
            Assert.That(controller.layers.Select(l => l.name), Is.EqualTo(new[] { "Base Layer", P.ConversationLayer, P.GestureLayer }));
        }

        [Test]
        public void Controller_HasAGestureStateForEveryClipBackedGesture()
        {
            var controller = (AnimatorController)_rig.Animator.runtimeAnimatorController;
            var machine = controller.layers.Single(l => l.name == P.GestureLayer).stateMachine;

            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (!GestureClips.TryGetClip(gesture, out var clip))
                    continue;
                var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == clip);
                Assert.That(state, Is.Not.Null, clip);
                Assert.That(state.tag, Is.EqualTo(P.GestureTag), clip);
                Assert.That(state.motion, Is.Not.Null, clip);
            }
        }
    }
}
#endif
