using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Tools
{
    public sealed class CaptureAndIdentifyToolTests
    {
        VisionFixture _f;

        [SetUp]
        public void SetUp() => _f = new VisionFixture();

        [TearDown]
        public void TearDown() => _f.Dispose();

        ToolContext ContextWith(EncodedImage preCaptured) =>
            ToolHarness.Context(new UserTurn { TurnId = 3, Text = "what is this", LanguageTag = "en", PreCaptured = preCaptured });

        [Test]
        public void FreshPreCapture_StreamsAnswerShowsMaskAndRemembersTheObject()
        {
            var image = _f.PreCapture();
            _f.Segmenter.Returns(ScriptedSegmenter.Square(24, 24, 16));
            var context = ContextWith(image);

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"what is this?\"}", context);

            Assert.That(_f.Camera.Captures, Is.EqualTo(1), "no second capture");
            Assert.That(_f.Camera.Released, Is.EqualTo(1));
            Assert.That(((RecordingSpeechSink)context.Speech).Text.ToString(), Is.EqualTo("It is a TV remote. Press the CC button."));
            Assert.That(result.OutputJson, Is.EqualTo("{\"object_id\":\"obj_1\",\"label\":\"TV remote\"," +
                                                      "\"answer\":\"It is a TV remote. Press the CC button.\",\"masked\":true}"));
            Assert.That(result.EndsTurn, Is.True);
            Assert.That(result.SpokenText, Is.Null);
            Assert.That(_f.Presenter.Shown.Single().distance, Is.EqualTo(2f).Within(1e-3f));
            Assert.That(Vector3.Distance(_f.Character.LastLook.Value.Point, new Vector3(0, 0, 2)), Is.LessThan(1e-3f));
            Assert.That(_f.Scene.Objects.Single().Label, Is.EqualTo("TV remote"));
            Assert.That(_f.Scene.LastCapture, Is.SameAs(image));
            Assert.That(_f.Identifier.LastRequest.PersonaInstructions, Is.EqualTo(context.PersonaInstructions));
            Assert.That(_f.Identifier.LastRequest.Question, Is.EqualTo("what is this?"));
        }

        [Test]
        public void StalePreCapture_CapturesAgain()
        {
            var image = _f.PreCapture();
            _f.Clock.Advance(TimeSpan.FromSeconds(3.5));

            ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(image));

            Assert.That(_f.Camera.Captures, Is.EqualTo(2));
        }

        [Test]
        public void CameraUnavailable_Fails()
        {
            _f.Camera.Availability = CameraAvailability.NoPermission;

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"camera_unavailable\"}"));
        }

        [Test]
        public void NoFrameWithinASecond_IsCameraUnavailable()
        {
            _f.Camera.FailNext = true;

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"camera_unavailable\"}"));
        }

        [Test]
        public void DarkFrame_IsTooDarkWithoutCallingSamOrSpark()
        {
            var earlier = _f.PreCapture();
            _f.Scene.LastCapture = earlier;
            _f.Camera.Fill(24);

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.OutputJson, Does.StartWith("{\"error\":\"too_dark\",\"detail\":"));
            Assert.That(result.SpokenText, Is.Null, "the model answers in character");
            Assert.That(_f.Segmenter.Prompts, Is.Empty);
            Assert.That(_f.Identifier.LastRequest, Is.Null);
            Assert.That(_f.Scene.LastCapture, Is.SameAs(earlier), "highlight_part never segments a dark frame");
            Assert.That(_f.Log.Contains(LogLevel.Info, "evt=vision.too_dark luma=24.0"), Is.True);
        }

        [Test]
        public void DarkPreCapture_IsTooDarkWithoutASecondCapture()
        {
            _f.Camera.Fill(10);
            var image = _f.PreCapture();

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(image));

            Assert.That(result.OutputJson, Does.Contain("too_dark"));
            Assert.That(_f.Camera.Captures, Is.EqualTo(1));
            Assert.That(_f.Identifier.LastRequest, Is.Null);
        }

        [Test]
        public void FrameAtTheThreshold_CallsThrough()
        {
            _f.Camera.Fill(25);
            _f.Segmenter.Returns(ScriptedSegmenter.Square(24, 24, 16));

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(result.Ok, Is.True);
            Assert.That(_f.Segmenter.Prompts, Is.Not.Empty);
            Assert.That(_f.Identifier.LastRequest, Is.Not.Null);
        }

        [Test]
        public void Pointing_SendsAPointPromptWithHandJointsAsNegatives()
        {
            _f.User.PointingRay = new Ray(Vector3.zero, Vector3.forward);
            _f.World.WallZ = 1.5f;
            _f.User.HandKeyPoints.Add(new Vector3(0.25f, 0, 1));
            _f.User.HandKeyPoints.Add(new Vector3(5f, 0, 1));
            _f.Segmenter.Returns(ScriptedSegmenter.Square(24, 24, 16));

            ToolHarness.Run(_f.Capture(), "{\"question\":\"q\",\"object_hint\":\"remote\"}", ContextWith(null));

            var prompt = _f.Segmenter.Prompts.Single();
            Assert.That(prompt.Kind, Is.EqualTo(SegmentPromptKind.Points));
            Assert.That(prompt.Points.Select(p => (p.X, p.Y, p.Positive)),
                Is.EqualTo(new[] { (32, 32, true), (48, 32, false) }));
        }

        [Test]
        public void NoPointing_UsesTheHintAsATextPrompt()
        {
            _f.Segmenter.Returns(ScriptedSegmenter.Square(24, 24, 16));

            ToolHarness.Run(_f.Capture(), "{\"question\":\"q\",\"object_hint\":\" remote \"}", ContextWith(null));

            Assert.That(_f.Segmenter.Prompts.Single().Text, Is.EqualTo("remote"));
        }

        [Test]
        public void NoPointingNoHint_UsesTheImageCentre()
        {
            ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            var first = _f.Segmenter.Prompts[0];
            Assert.That(first.Points.Single().X, Is.EqualTo(32));
            Assert.That(first.Points.Single().Y, Is.EqualTo(32));
        }

        [Test]
        public void ImplausibleMask_RetriesOnceWithTheHeaderNounPhrase()
        {
            _f.Segmenter.Returns(ScriptedSegmenter.Square(0, 0, 60)).Returns(ScriptedSegmenter.Square(24, 24, 16));

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(_f.Segmenter.Prompts.Count, Is.EqualTo(2));
            Assert.That(_f.Segmenter.Prompts[1].Text, Is.EqualTo("remote"));
            Assert.That((bool)ToolHarness.Output(result)["masked"], Is.True);
            Assert.That(_f.Presenter.Shown.Single().objects.Single().Box.width, Is.EqualTo(16));
        }

        [Test]
        public void HandInsideMask_IsImplausible()
        {
            _f.User.HandKeyPoints.Add(new Vector3(0, 0, 1));
            _f.Segmenter.Returns(ScriptedSegmenter.Square(24, 24, 16)).Returns();

            ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(_f.Segmenter.Prompts.Count, Is.EqualTo(2));
            Assert.That(_f.Presenter.Shown.Single().objects.Single().Box.x, Is.EqualTo(24), "an empty retry keeps the first mask");
        }

        [Test]
        public void SamFailure_StillAnswersWithoutAMask()
        {
            _f.Segmenter.Failure = new ProviderException("sam", ProviderErrorKind.Server, "boom");

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            var output = ToolHarness.Output(result);
            Assert.That(result.Ok, Is.True);
            Assert.That((bool)output["masked"], Is.False);
            Assert.That(output["object_id"].Type, Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));
            Assert.That(_f.Presenter.Shown, Is.Empty);
        }

        [Test]
        public void SamAndVisionRunInParallel()
        {
            _f.Segmenter.Manual = true;
            _f.Segmenter.Returns(ScriptedSegmenter.Square(24, 24, 16));
            _f.Identifier.Gate = true;

            var task = ToolHarness.Start(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(_f.Segmenter.Prompts.Count, Is.EqualTo(1));
            Assert.That(_f.Identifier.LastRequest, Is.Not.Null);
            _f.Segmenter.Release();
            Assert.That(_f.Presenter.Shown.Count, Is.EqualTo(1), "mask shown before the answer finished");
            _f.Identifier.Release();
            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void VisionTimeout_SpeaksTheHoldCloserLine()
        {
            _f.Identifier.Gate = true;

            var task = ToolHarness.Start(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));
            _f.Clock.Advance(TimeSpan.FromSeconds(8));
            var result = Sync.Run(task);

            Assert.That(result.Ok, Is.False);
            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"vision_timeout\"}"));
            Assert.That(result.SpokenText, Is.EqualTo("I couldn't see it clearly, can you hold it closer?"));
            Assert.That(result.EndsTurn, Is.True);
        }

        [Test]
        public void EmptyVisionAnswer_SpeaksTheHoldCloserLineInsteadOfEndingSilently()
        {
            _f.Identifier.Answer = Array.Empty<string>();

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"vision_empty\"}"));
            Assert.That(result.SpokenText, Is.EqualTo("I couldn't see it clearly, can you hold it closer?"));
            Assert.That(result.EndsTurn, Is.True);
        }

        [Test]
        public void VisionProviderError_IsReportedToTheModel()
        {
            _f.Identifier.Failure = new ProviderException("vision", ProviderErrorKind.RateLimited, "429");

            var result = ToolHarness.Run(_f.Capture(), "{\"question\":\"q\"}", ContextWith(null));

            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"vision_error\",\"detail\":\"RateLimited\"}"));
            Assert.That(result.EndsTurn, Is.False);
        }
    }
}
