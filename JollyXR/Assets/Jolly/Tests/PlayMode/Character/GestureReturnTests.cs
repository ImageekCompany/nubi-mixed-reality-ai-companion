#if UNITY_EDITOR
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>
    /// However a gesture ends (played out, or cancelled midway by a barge-in), the character goes back to its idle pose.
    /// A dance left the arms up until the next walk on the headset (2026-11-02). Measured on the hands, the bones that
    /// travel furthest, in the character's own space.
    /// </summary>
    public sealed class GestureReturnTests
    {
        const string Jolly = "Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset";
        const float IdleToleranceMeters = 0.04f;
        const float SettleSeconds = 1.5f;

        static readonly GestureId[] All = (GestureId[])Enum.GetValues(typeof(GestureId));

        static CharacterTestRig Rig(bool jolly)
        {
            CharacterDefinition character = null;
            if (jolly)
            {
                character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Jolly);
                if (character == null)
                    Assert.Ignore("no local Jolly character on this checkout");
            }
            var rig = CharacterTestRig.Create(character: character);
            // Batchmode renders nothing, and the default culling would freeze the bones this test reads.
            rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rig.User.Head = new Pose(rig.Nubi.Position + Vector3.forward * 1.5f + Vector3.up * 1.6f, Quaternion.LookRotation(Vector3.back));
            return rig;
        }

        static Vector3 Hand(CharacterTestRig rig, string name) =>
            rig.Nubi.transform.InverseTransformPoint(CharacterRig.Find(rig.Nubi.transform, name).position);

        static float OffIdle(CharacterTestRig rig, Vector3 left, Vector3 right) =>
            Mathf.Max(Vector3.Distance(Hand(rig, "Hand_L"), left), Vector3.Distance(Hand(rig, "Hand_R"), right));

        static async UniTask AssertBackAtIdle(CharacterTestRig rig, string what, Vector3 left, Vector3 right)
        {
            await CharacterTestRig.Until(() => OffIdle(rig, left, right) < IdleToleranceMeters, SettleSeconds);
            Assert.That(OffIdle(rig, left, right), Is.LessThan(IdleToleranceMeters), what + ": the hands did not return to idle");
        }

        [UnityTest]
        public IEnumerator Gesture_PlayedOut_ReturnsToIdle([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var left = Hand(rig, "Hand_L");
            var right = Hand(rig, "Hand_R");
            foreach (var gesture in All)
            {
                if (gesture == GestureId.LookAround || !rig.Nubi.CanPlay(gesture))
                    continue;
                await rig.Nubi.PlayGesture(gesture, CancellationToken.None);
                await AssertBackAtIdle(rig, gesture.ToString(), left, right);
            }
        });

        [UnityTest]
        public IEnumerator Gesture_CancelledMidway_ReturnsToIdle([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var left = Hand(rig, "Hand_L");
            var right = Hand(rig, "Hand_R");
            foreach (var gesture in All)
            {
                if (gesture == GestureId.LookAround || !rig.Nubi.CanPlay(gesture))
                    continue;
                using var cts = new CancellationTokenSource();
                var play = rig.Nubi.PlayGesture(gesture, cts.Token).SuppressCancellationThrow();
                await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
                cts.Cancel();
                await play;
                await AssertBackAtIdle(rig, gesture + " cancelled", left, right);
            }
        });

        [UnityTest]
        public IEnumerator ConversationPose_Left_ReturnsToIdle([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var left = Hand(rig, "Hand_L");
            var right = Hand(rig, "Hand_R");
            foreach (var state in new[] { ConversationState.Thinking, ConversationState.Listening, ConversationState.Speaking })
            {
                rig.Nubi.SetConversationState(state);
                await UniTask.Delay(TimeSpan.FromSeconds(0.8f));
                rig.Nubi.SetConversationState(ConversationState.Idle);
                await AssertBackAtIdle(rig, state.ToString(), left, right);
            }
        });

        [UnityTest]
        public IEnumerator Dance_PlayedOut_ReturnsToIdle([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var left = Hand(rig, "Hand_L");
            var right = Hand(rig, "Hand_R");

            await rig.Nubi.PlayLooped(GestureId.Dance, 2f, CancellationToken.None);

            await AssertBackAtIdle(rig, "Dance", left, right);
        });

        [UnityTest]
        public IEnumerator Dance_CancelledMidway_ReturnsToIdle([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var left = Hand(rig, "Hand_L");
            var right = Hand(rig, "Hand_R");
            using var cts = new CancellationTokenSource();

            var play = rig.Nubi.PlayLooped(GestureId.Dance, 4f, cts.Token).SuppressCancellationThrow();
            await UniTask.Delay(TimeSpan.FromSeconds(1.5f));
            cts.Cancel();
            await play;

            await AssertBackAtIdle(rig, "Dance cancelled", left, right);
        });
    }
}
#endif
