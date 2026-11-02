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

        [Test]
        public void CanPlay_Nubi_HasEveryGestureIncludingTheActions()
        {
            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
                Assert.That(_rig.Nubi.CanPlay(gesture), Is.True, gesture.ToString());
        }

        [UnityTest]
        public IEnumerator PlayGesture_Dance_PlaysOneLoopOnTheActionLayerThenReturnsToEmpty() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var layer = _rig.Animator.GetLayerIndex(P.ActionLayer);
            var started = Time.time;
            var danced = false;

            // Act
            var dance = _rig.Nubi.PlayGesture(GestureId.Dance, CancellationToken.None);
            while (dance.Status == UniTaskStatus.Pending)
            {
                danced |= _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName("Dance");
                await UniTask.Yield();
            }

            // Assert
            Assert.That(danced, Is.True);
            Assert.That(Time.time - started, Is.InRange(3.3f, 4.6f), "one 4 s loop");
            Assert.That(await CharacterTestRig.Until(
                () => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), 0.5f), Is.True);
        });

        [UnityTest]
        public IEnumerator PlayLooped_DanceHeldFiveSeconds_EndsAtTheEndOfTheSecondLoop() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var started = Time.time;

            // Act
            await _rig.Nubi.PlayLooped(GestureId.Dance, 5f, CancellationToken.None);

            // Assert
            Assert.That(Time.time - started, Is.InRange(7.3f, 8.6f), "two 4 s loops, never a cut mid-loop");
            Assert.That(_rig.Animator.GetBool(P.GestureHold), Is.False);
        });

        [UnityTest]
        public IEnumerator PlayLooped_DanceForFourSeconds_EndsAtTheEndOfTheFirstLoop() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: the clip loops every 4 s; the hold used to run into a second loop (7.8 s).
            var started = Time.time;

            // Act
            await _rig.Nubi.PlayLooped(GestureId.Dance, 4f, CancellationToken.None);

            // Assert
            Assert.That(Time.time - started, Is.InRange(3.4f, 4.6f));
        });

        [UnityTest]
        public IEnumerator PlayGesture_NodDuringADance_LeavesTheDanceRunningToItsEnd() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var action = _rig.Animator.GetLayerIndex(P.ActionLayer);
            var started = Time.time;
            var dance = _rig.Nubi.PlayLooped(GestureId.Dance, 5f, CancellationToken.None);
            await CharacterTestRig.Until(() => _rig.Animator.GetCurrentAnimatorStateInfo(action).IsName("Dance"), 1f);

            // Act
            await _rig.Nubi.PlayGesture(GestureId.Nod, CancellationToken.None);
            var dancingAfterNod = _rig.Animator.GetCurrentAnimatorStateInfo(action).IsName("Dance");
            await dance;

            // Assert
            Assert.That(dancingAfterNod, Is.True);
            Assert.That(Time.time - started, Is.InRange(7.3f, 8.6f), "both loops of the held dance");
        });

        [UnityTest]
        public IEnumerator PlayLooped_Cancelled_FadesTheDanceOut() => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            var layer = _rig.Animator.GetLayerIndex(P.ActionLayer);
            using var cts = new CancellationTokenSource();
            var dance = _rig.Nubi.PlayLooped(GestureId.Dance, 10f, cts.Token);
            await CharacterTestRig.Until(() => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName("Dance"), 1f);

            // Act
            cts.Cancel();

            // Assert
            var threw = false;
            try { await dance; }
            catch (OperationCanceledException) { threw = true; }
            Assert.That(threw, Is.True);
            Assert.That(_rig.Animator.GetBool(P.GestureHold), Is.False);
            Assert.That(await CharacterTestRig.Until(
                () => _rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), 1f), Is.True);
        });

        [UnityTest]
        public IEnumerator PlayGesture_Jump_LiftsBothFeetOffTheFloor() => UniTask.ToCoroutine(async () =>
        {
            // Arrange: on the upper-body Gesture layer the legs would stay planted. Batch mode renders nothing, so the
            // default CullUpdateTransforms would never write the bones.
            _rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            await UniTask.DelayFrame(2);
            var feet = _rig.Nubi.ShadowRig;
            var rest = Mathf.Min(feet.footLeft.position.y, feet.footRight.position.y);
            var highest = 0f;

            // Act
            var jump = _rig.Nubi.PlayGesture(GestureId.Jump, CancellationToken.None);
            while (jump.Status == UniTaskStatus.Pending)
            {
                highest = Mathf.Max(highest, Mathf.Min(feet.footLeft.position.y, feet.footRight.position.y) - rest);
                await UniTask.Yield();
            }

            // Assert
            Assert.That(highest, Is.GreaterThan(0.1f), "the clip clears 0.2 m at the character's own scale");
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
                P.MoodName, P.GestureIdName, P.GestureName, P.GestureHoldName,
            }));
            Assert.That(controller.layers.Select(l => l.name),
                        Is.EqualTo(new[] { "Base Layer", P.ConversationLayer, P.GestureLayer, P.ActionLayer }));
            Assert.That(controller.layers.Single(l => l.name == P.ActionLayer).avatarMask, Is.Null, "full body");
        }

        [Test]
        public void Controller_HasAGestureStateForEveryClipBackedGesture()
        {
            var controller = (AnimatorController)_rig.Animator.runtimeAnimatorController;

            foreach (GestureId gesture in Enum.GetValues(typeof(GestureId)))
            {
                if (!GestureClips.TryGetClip(gesture, out var clip))
                    continue;
                var layer = GestureClips.IsFullBody(gesture) ? P.ActionLayer : P.GestureLayer;
                var machine = controller.layers.Single(l => l.name == layer).stateMachine;
                var state = machine.states.Select(s => s.state).SingleOrDefault(s => s.name == clip);
                Assert.That(state, Is.Not.Null, clip);
                Assert.That(state.tag, Is.EqualTo(P.GestureTag), clip);
                Assert.That(state.motion, Is.Not.Null, clip);
            }
        }
    }
}
#endif
