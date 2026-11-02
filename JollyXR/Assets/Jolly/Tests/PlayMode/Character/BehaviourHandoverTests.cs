#if UNITY_EDITOR
using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using P = Jolly.Character.NubiAnimatorParameters;

namespace Jolly.Tests.PlayMode.Character
{
    /// <summary>One behaviour handing over to another on both characters: no dead pose, no dip, no stuck layer.</summary>
    public sealed class BehaviourHandoverTests
    {
        const string JollyAsset = "Assets/Jolly/Local/Characters/Jolly/JollyCharacter.asset";
        /// <summary>The Think gesture and the Think pose are one clip; they differ by this much once settled.</summary>
        const float SamePoseMeters = 0.03f;
        const float PoseToleranceMeters = 0.05f;

        static CharacterTestRig Rig(bool jolly)
        {
            CharacterDefinition character = null;
            if (jolly)
            {
                character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(JollyAsset);
                if (character == null)
                    Assert.Ignore("no local Jolly character on this checkout");
            }
            var rig = CharacterTestRig.Create(character: character);
            rig.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rig.User.Head = new Pose(rig.Nubi.Position + Vector3.forward * 1.5f + Vector3.up * 1.6f, Quaternion.LookRotation(Vector3.back));
            return rig;
        }

        readonly struct Hands
        {
            public Hands(CharacterTestRig rig)
            {
                var root = rig.Nubi.transform;
                Left = root.InverseTransformPoint(CharacterRig.Find(root, "Hand_L").position);
                Right = root.InverseTransformPoint(CharacterRig.Find(root, "Hand_R").position);
            }

            public Vector3 Left { get; }
            public Vector3 Right { get; }
            public float From(Hands other) => Mathf.Max(Vector3.Distance(Left, other.Left), Vector3.Distance(Right, other.Right));
        }

        static async UniTask<Hands> PoseOf(CharacterTestRig rig, ConversationState state)
        {
            rig.Nubi.SetConversationState(state);
            await UniTask.Delay(TimeSpan.FromSeconds(1.2f));
            return new Hands(rig);
        }

        [UnityTest]
        public IEnumerator SearchEndsInActing_TheHandsStayInTheThinkPose([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var think = await PoseOf(rig, ConversationState.Thinking);
            await PoseOf(rig, ConversationState.Acting);
            var events = new ConversationEvents();
            using var search = new SearchThinkGesture(events, rig.Nubi);
            events.RaiseStateChanged(ConversationState.Thinking, ConversationState.Acting);
            events.RaiseToolStarted("ws_1", ToolNames.WebSearch);
            await UniTask.Delay(TimeSpan.FromSeconds(0.8f));

            // Act: the pipeline's order when the search ends
            events.RaiseToolCompleted("ws_1", ToolNames.WebSearch, true);
            rig.Nubi.SetConversationState(ConversationState.Thinking);
            events.RaiseStateChanged(ConversationState.Acting, ConversationState.Thinking);
            var worst = 0f;
            var end = Time.time + 0.8f;
            while (Time.time < end)
            {
                await UniTask.Yield();
                worst = Mathf.Max(worst, new Hands(rig).From(think));
            }

            // Assert
            Assert.That(worst, Is.LessThan(SamePoseMeters), "the hands dipped at the hand-over");
        });

        [UnityTest]
        public IEnumerator DanceEndsWhileSpeaking_TheTalkPoseTakesOver([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var talk = await PoseOf(rig, ConversationState.Speaking);
            var layer = rig.Animator.GetLayerIndex(P.ActionLayer);

            // Act
            await rig.Nubi.PlayLooped(GestureId.Dance, 2f, CancellationToken.None);
            await CharacterTestRig.Until(() => !rig.Animator.IsInTransition(layer) && new Hands(rig).From(talk) < PoseToleranceMeters, 1f);

            // Assert
            Assert.That(rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), Is.True);
            Assert.That(new Hands(rig).From(talk), Is.LessThan(PoseToleranceMeters), "the Talk pose came back after the move");
        });

        [UnityTest]
        public IEnumerator GestureDuringTalk_TheTalkPoseComesBack([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var talk = await PoseOf(rig, ConversationState.Speaking);

            // Act
            await rig.Nubi.PlayGesture(GestureId.Wave, CancellationToken.None);
            await CharacterTestRig.Until(() => new Hands(rig).From(talk) < PoseToleranceMeters, 1f);

            // Assert
            Assert.That(new Hands(rig).From(talk), Is.LessThan(PoseToleranceMeters));
        });

        [UnityTest]
        public IEnumerator WalkStartsDuringADance_TheMoveIsGoneWithinAFade([Values(false, true)] bool jolly) => UniTask.ToCoroutine(async () =>
        {
            // Arrange
            using var rig = Rig(jolly);
            await UniTask.DelayFrame(10);
            var layer = rig.Animator.GetLayerIndex(P.ActionLayer);
            var dance = rig.Nubi.PlayLooped(GestureId.Dance, 4f, CancellationToken.None).Preserve();
            await CharacterTestRig.Until(() => rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName("Dance"), 2f);
            await UniTask.Delay(TimeSpan.FromSeconds(0.5f));

            // Act
            Assert.That(rig.Nubi.TryStartMoveTo(new Vector3(1.5f, 0f, -1.5f), 0.1f, out _), Is.True);
            var at = Time.time;
            var empty = await CharacterTestRig.Until(() =>
                !rig.Animator.IsInTransition(layer) && rig.Animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState), 1f);
            var seconds = Time.time - at;
            await dance;

            // Assert
            Assert.That(empty, Is.True);
            Assert.That(seconds, Is.LessThan(0.3f));
            Assert.That(rig.Nubi.IsMoving, Is.True, "walking from the first frame, no dead time");
        });
    }
}
#endif
