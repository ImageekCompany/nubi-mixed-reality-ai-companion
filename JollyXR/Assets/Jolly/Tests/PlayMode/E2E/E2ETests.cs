using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;
using Jolly.Tests.PlayMode.E2E.Doubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kind = Jolly.Tests.PlayMode.E2E.Doubles.RoutingHttpTransport.Kind;

namespace Jolly.Tests.PlayMode.E2E
{
    /// <summary>The shipped scene end to end, with doubles only at the provider edges (WP9 row of scope.md).</summary>
    public sealed class E2ETests
    {
        const double TurnSeconds = 15;

        [UnityTest]
        public IEnumerator VoiceTurn_ListensThinksCallsTheToolAndSpeaksWithCaptions() => UniTask.ToCoroutine(async () =>
        {
            var app = await AppHarness.Start();
            try
            {
                app.Asr.Will("Nubi, come here.");
                app.Http.Enqueue(Kind.Chat, new ResponsesScript().Say("On my way!").Call(ToolNames.ComeHere, "{}").Complete())
                        .Enqueue(Kind.Chat, new ResponsesScript().Say("Here ", "I am.").Complete());

                await app.Talk();
                Assert.That(await app.WaitUntil(() => app.States.Count > 2, TurnSeconds), Is.True, app.StatePath());
                Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());

                Assert.That(app.UserFinal, Is.EqualTo("Nubi, come here."));
                Assert.That(app.StatePath(), Does.StartWith("Listening>Thinking"));
                Assert.That(app.States, Has.Member(ConversationState.Speaking));
                Assert.That(app.States[app.States.Count - 1], Is.EqualTo(ConversationState.Idle));
                Assert.That(app.ToolsStarted, Is.EqualTo(new[] { ToolNames.ComeHere }));
                Assert.That(app.ToolsCompleted, Has.Count.EqualTo(1));
                Assert.That(app.Captions, Has.Member("On my way!").And.Member("Here I am."));
                Assert.That(app.Tts.Texts, Has.Member("On my way!").And.Member("Here I am."));
                Assert.That(app.Http.Count(Kind.Chat), Is.EqualTo(2), "a second round answers the tool output");
                Assert.That(app.Asr.AudioFrames, Is.GreaterThan(0), "microphone audio reached the ASR socket");
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator BargeIn_SilencesWithin150MsAndStartsListening() => UniTask.ToCoroutine(async () =>
        {
            var app = await AppHarness.Start();
            try
            {
                app.Asr.Will("Tell me a long story.").Will(string.Empty);
                app.Http.Enqueue(Kind.Chat, new ResponsesScript()
                    .Say("Once upon a time, a little sailor set out across a quiet sea. ",
                         "The waves were gentle and the stars were bright above the boat. ",
                         "Every night the sailor sang to the moon until the morning came.")
                    .Complete());
                await app.Talk();
                Assert.That(await app.WaitUntil(() => app.State == ConversationState.Speaking, TurnSeconds), Is.True,
                    app.StatePath());

                var pressedAt = Time.realtimeSinceStartupAsDouble;
                app.Voice.Press();
                var silent = await app.WaitUntil(() => !app.Speaker.IsPlaying, 1);
                var silentMs = (Time.realtimeSinceStartupAsDouble - pressedAt) * 1000;
                app.Voice.Release();

                Assert.That(silent, Is.True, "the speaker went silent");
                Assert.That(silentMs, Is.LessThanOrEqualTo(150));
                Assert.That(app.Speaker.FlushCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(app.States, Has.Member(ConversationState.Listening));
                Assert.That(app.StatePath(), Does.Contain("Speaking>Listening"));
                Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator CaptureAndIdentify_SegmentsTheStillFrameAndSpeaksTheVisionAnswer() => UniTask.ToCoroutine(async () =>
        {
            var app = await AppHarness.Start();
            try
            {
                app.Asr.Will("What is this and how do I use it?");
                app.Http.Enqueue(Kind.Chat, new ResponsesScript().Say("Let me look.")
                        .Call(ToolNames.CaptureAndIdentify, "{\"question\":\"What is this and how do I use it?\"}").Complete())
                    .Enqueue(Kind.Sam, ResponsesScript.Sam(Fixture("sam/mug_concept_onebit.response.json")))
                    .Enqueue(Kind.Vision, new ResponsesScript()
                        .Say("White mug|mug\n", "That's a coffee mug. ", "Hold it by the handle and sip slowly.").Complete());

                await app.Talk();
                Assert.That(await app.WaitUntil(() => app.ToolsCompleted.Count > 0, TurnSeconds), Is.True, app.StatePath());
                Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());

                Assert.That(app.ToolsCompleted, Is.EqualTo(new[] { ToolNames.CaptureAndIdentify }));
                Assert.That(app.Http.Count(Kind.Sam), Is.GreaterThanOrEqualTo(1));
                Assert.That(app.Http.Count(Kind.Vision), Is.EqualTo(1));
                Assert.That(app.Http.Count(Kind.Chat), Is.EqualTo(1), "EndsTurn skips the next model round");
                Assert.That(string.Join(" ", app.Captions), Does.Contain("coffee mug"));
                var scene = app.Graph.Conversation.Scene;
                Assert.That(scene.Objects, Has.Count.EqualTo(1));
                Assert.That(scene.Objects[0].Label, Does.Contain("mug").IgnoreCase);
            }
            finally
            {
                await app.Unload();
            }
        });

        [UnityTest]
        public IEnumerator Offline_PressPlaysACannedLineWithoutTheCloud() => UniTask.ToCoroutine(async () =>
        {
            var app = await AppHarness.Start(online: false);
            try
            {
                Assert.That(await app.WaitUntil(() => app.Graph.Cloud.Connectivity.State == Connectivity.Offline, 5), Is.True,
                    app.Graph.Cloud.Connectivity.State.ToString());

                await app.Talk();
                Assert.That(await app.WaitUntil(() => app.Captions.Count > 0, 2), Is.True, "a caption within 2 s");
                Assert.That(await app.WaitForIdle(TurnSeconds), Is.True, app.StatePath());

                Assert.That(app.StatePath(), Does.StartWith("Speaking"), "an offline press skips Listening");
                Assert.That(app.Http.Requests, Is.Empty);
                Assert.That(app.Asr.Sessions, Is.Zero);
            }
            finally
            {
                await app.Unload();
            }
        });

        static string Fixture(string relative) =>
            File.ReadAllText(Path.Combine(Application.dataPath, "Jolly/Tests/Fixtures", relative));
    }
}
