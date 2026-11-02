#if UNITY_EDITOR
using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Tests.PlayMode.E2E.Doubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kind = Jolly.Tests.PlayMode.E2E.Doubles.RoutingHttpTransport.Kind;
using P = Jolly.Character.NubiAnimatorParameters;

namespace Jolly.Tests.PlayMode.E2E
{
    /// <summary>
    /// A body move through the shipped scene's real controller, pipeline, agent, tools and Animator: a pinch stops it
    /// mid-move and the hands return to idle, and no dead time follows it. A dance stopped midway on the headset left the
    /// arms up (2026-11-02).
    /// </summary>
    public sealed class BodyMoveFlowTests
    {
        const double TurnSeconds = 15;
        /// <summary>The 150 ms fade, the frame that sees the cancel, and an editor's slow frames.</summary>
        const double MaxFadeMs = 300;
        const float IdleToleranceMeters = 0.04f;
        const string ModelPhrasing = "Nubi, would you do that move for me today?";
        /// <summary>Outside the guard's tables, and one short clause, so a move with words before it ends the turn.</summary>
        const string ShortModelPhrasing = "Would you do that move?";

        static async UniTask<AppHarness> Ready()
        {
            var app = await AppHarness.Start();
            // Batchmode renders nothing, and the default culling would freeze the bones these tests read.
            app.Graph.Scene.nubi.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());
            await UniTask.Delay(TimeSpan.FromSeconds(0.5), DelayType.Realtime);
            return app;
        }

        static Vector3 Hand(NubiCharacterController nubi, string bone) =>
            nubi.transform.InverseTransformPoint(CharacterRig.Find(nubi.transform, bone).position);

        static bool IsEmpty(Animator animator, int layer) =>
            !animator.IsInTransition(layer) && animator.GetCurrentAnimatorStateInfo(layer).IsName(P.EmptyState);

        static async UniTask BargesInMidMove(AppHarness app, string state, double playedSeconds)
        {
            var nubi = app.Graph.Scene.nubi;
            var animator = nubi.Animator;
            var layer = animator.GetLayerIndex(P.ActionLayer);
            var left = Hand(nubi, "Hand_L");
            var right = Hand(nubi, "Hand_R");

            await app.Talk();
            Assert.That(await app.WaitUntil(() => animator.GetCurrentAnimatorStateInfo(layer).IsName(state), TurnSeconds), Is.True,
                app.StatePath());
            await UniTask.Delay(TimeSpan.FromSeconds(playedSeconds), DelayType.Realtime);
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsName(state), Is.True, "still mid-move");

            var pressedAt = Time.realtimeSinceStartupAsDouble;
            app.Voice.Press();
            var gone = await app.WaitUntil(() => IsEmpty(animator, layer), 2);
            var fadeMs = (Time.realtimeSinceStartupAsDouble - pressedAt) * 1000;
            app.Voice.Release();

            Assert.That(gone, Is.True, "the Action layer emptied");
            Assert.That(fadeMs, Is.LessThanOrEqualTo(MaxFadeMs));
            Assert.That(app.Speaker.IsPlaying, Is.False, "speech stopped too");
            Assert.That(app.State, Is.Not.EqualTo(ConversationState.Acting));
            Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());
            Assert.That(await app.WaitUntil(() =>
                Vector3.Distance(Hand(nubi, "Hand_L"), left) < IdleToleranceMeters
                && Vector3.Distance(Hand(nubi, "Hand_R"), right) < IdleToleranceMeters, 2), Is.True, "the hands returned to idle");
            Assert.That(IsEmpty(animator, layer), Is.True);
        }

        [UnityTest]
        public IEnumerator BargeIn_DuringTheModelsMove_FadesToIdle(
            [Values(ToolNames.Actions.Dance, ToolNames.Actions.Jump, ToolNames.Actions.Bow)] string tool) => UniTask.ToCoroutine(async () =>
        {
            var app = await Ready();
            try
            {
                // Arrange
                app.Asr.Will(ModelPhrasing).Will(string.Empty);
                app.Http.Enqueue(Kind.Chat, new ResponsesScript().Say("Let me try.").Call(tool, "{}").Complete());

                // Act + Assert
                await BargesInMidMove(app, tool.Substring(tool.IndexOf('.') + 1) switch
                {
                    "dance" => "Dance",
                    "jump" => "Jump",
                    _ => "Bow",
                }, 0.3);
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator BargeIn_DuringAGuardedDance_FadesToIdle() => UniTask.ToCoroutine(async () =>
        {
            var app = await Ready();
            try
            {
                // Arrange
                app.Asr.Will("Dance.").Will(string.Empty);

                // Act + Assert
                await BargesInMidMove(app, "Dance", 1.0);
                Assert.That(app.Http.Count(Kind.Chat), Is.Zero);
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator BargeIn_DuringASpin_StopsItAndAttentionTurnsTheBodyBack() => UniTask.ToCoroutine(async () =>
        {
            var app = await Ready();
            try
            {
                // Arrange
                var nubi = app.Graph.Scene.nubi;
                nubi.TryMeasureUser(out var bodyBefore, out _);
                app.Asr.Will("Spin around.").Will(string.Empty);
                await app.Talk();
                Assert.That(await app.WaitUntil(() =>
                {
                    nubi.TryMeasureUser(out var off, out _);
                    return off > bodyBefore + 60f;
                }, TurnSeconds), Is.True, app.StatePath());

                // Act
                app.Voice.Press();

                // Assert: within the fade the root stops being driven by the spin, then attention brings it round.
                await UniTask.DelayFrame(2);
                Assert.That(nubi.Attending, Is.EqualTo(AttentionTarget.User), "the spin no longer holds attention off");
                var turnedBack = await app.WaitUntil(() =>
                {
                    nubi.TryMeasureUser(out var off, out _);
                    return off <= 25f;
                }, 3);
                app.Voice.Release();
                Assert.That(turnedBack, Is.True, "the body faced the user again");
                Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator ModelsMove_SpeaksDuringItAndTheTurnEndsWhenItEnds() => UniTask.ToCoroutine(async () =>
        {
            var app = await Ready();
            try
            {
                // Arrange
                var animator = app.Graph.Scene.nubi.Animator;
                var layer = animator.GetLayerIndex(P.ActionLayer);
                app.Asr.Will(ShortModelPhrasing);
                app.Http.Enqueue(Kind.Chat, new ResponsesScript().Say("Let me try.").Call(ToolNames.Actions.Jump, "{}").Complete());

                // Act
                await app.Talk();
                Assert.That(await app.WaitUntil(() => animator.GetCurrentAnimatorStateInfo(layer).IsName("Jump"), TurnSeconds), Is.True,
                    app.StatePath());
                var spokeDuring = await app.WaitUntil(() => app.Tts.Texts.Contains("Let me try."), 0.5);
                Assert.That(await app.WaitUntil(() => IsEmpty(animator, layer), TurnSeconds), Is.True);
                var moveEnded = Time.realtimeSinceStartupAsDouble;
                var idle = await app.WaitForIdle(TurnSeconds);
                var deadSeconds = Time.realtimeSinceStartupAsDouble - moveEnded;

                // Assert
                Assert.That(spokeDuring, Is.True, "the pre-text goes to speech as the move starts, not after it");
                Assert.That(idle, Is.True, app.StatePath());
                Assert.That(app.Http.Count(Kind.Chat), Is.EqualTo(1), "no closing round");
                Assert.That(deadSeconds, Is.LessThan(0.5), "no dead time after the move");
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator GuardedCommand_StartsTheMoveInTheFrameOfTheFinal() => UniTask.ToCoroutine(async () =>
        {
            var app = await Ready();
            try
            {
                // Arrange
                var animator = app.Graph.Scene.nubi.Animator;
                var layer = animator.GetLayerIndex(P.ActionLayer);
                app.Asr.Will("Jump!");
                var heardAt = 0.0;
                app.Graph.Foundation.Events.UserFinal += _ => heardAt = Time.realtimeSinceStartupAsDouble;

                // Act
                await app.Talk();
                Assert.That(await app.WaitUntil(() => animator.GetCurrentAnimatorStateInfo(layer).IsName("Jump"), TurnSeconds), Is.True,
                    app.StatePath());
                var startMs = (Time.realtimeSinceStartupAsDouble - heardAt) * 1000;
                Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());

                // Assert
                Assert.That(startMs, Is.LessThan(250), "the move is on within a couple of frames of the final");
                Assert.That(app.ToolsStarted, Is.EqualTo(new[] { ToolNames.Actions.Jump }));
                Assert.That(app.Http.Count(Kind.Chat), Is.Zero);
                Assert.That(app.Captions, Is.Empty);
            }
            finally
            {
                await app.Unload();
            }
        });
    }
}
#endif
