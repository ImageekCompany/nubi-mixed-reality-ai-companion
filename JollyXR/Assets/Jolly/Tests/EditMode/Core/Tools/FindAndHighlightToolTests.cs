using System.Linq;
using Jolly.Contracts;
using Jolly.Testing.Conversation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Core.Tools
{
    public sealed class FindAndHighlightToolTests
    {
        VisionFixture _f;

        [SetUp]
        public void SetUp() => _f = new VisionFixture();

        [TearDown]
        public void TearDown() => _f.Dispose();

        [Test]
        public void FindObject_OutlinesEveryInstanceAndLooksAtTheNearest()
        {
            _f.User.Head = new Pose(new Vector3(-1, 0, 0), Quaternion.identity);
            _f.Segmenter.Returns(ScriptedSegmenter.Square(40, 24, 8, id: 1), ScriptedSegmenter.Square(4, 24, 8, id: 2));

            var result = ToolHarness.Run(_f.Find(), "{\"noun_phrase\":\"reading glasses\"}");

            var output = ToolHarness.Output(result);
            Assert.That((int)output["found"], Is.EqualTo(2));
            Assert.That((string)output["nearest_id"], Is.EqualTo("obj_1"));
            Assert.That(_f.Segmenter.Prompts.Single().Text, Is.EqualTo("reading glasses"));
            Assert.That(_f.Presenter.Shown.Single().objects.Count, Is.EqualTo(2));
            Assert.That(_f.Character.LastLook.Value.Point.x, Is.LessThan(0), "the left instance is nearer the user");
            Assert.That(_f.Scene.Objects.Single().Label, Is.EqualTo("reading glasses"));
            Assert.That(_f.Scene.LastCapture, Is.Not.Null);
        }

        [Test]
        public void FindObject_MoreInstancesThanTheCap_OutlinesOnlyTheFirstOnesAndKeepsTheNearest()
        {
            _f.Settings.MaxInstances = 2;
            _f.User.Head = new Pose(new Vector3(-1, 0, 0), Quaternion.identity);
            _f.Segmenter.Returns(ScriptedSegmenter.Square(4, 24, 8, id: 1), ScriptedSegmenter.Square(40, 24, 8, id: 2),
                ScriptedSegmenter.Square(56, 24, 8, id: 3));

            var output = ToolHarness.Output(ToolHarness.Run(_f.Find(), "{\"noun_phrase\":\"keys\"}"));

            Assert.That((int)output["found"], Is.EqualTo(2));
            Assert.That(_f.Presenter.Shown.Single().objects.Select(o => o.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_f.Character.LastLook.Value.Point.x, Is.LessThan(0), "the first, left instance stays the nearest");
            Assert.That((float)output["distance_m"],
                Is.EqualTo(Vector3.Distance(_f.User.Head.position, _f.Character.LastLook.Value.Point)).Within(0.01f));
        }

        [Test]
        public void FindObject_NothingFound()
        {
            var result = ToolHarness.Run(_f.Find(), "{\"noun_phrase\":\"keys\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"found\":0}"));
            Assert.That(_f.Presenter.Shown, Is.Empty);
        }

        [Test]
        public void FindObject_CameraUnavailable()
        {
            _f.Camera.Availability = CameraAvailability.Starting;

            var result = ToolHarness.Run(_f.Find(), "{\"noun_phrase\":\"keys\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"error\":\"camera_unavailable\"}"));
        }

        [Test]
        public void HighlightPart_UsesTheLastCaptureWithoutANewOne()
        {
            _f.Scene.LastCapture = _f.PreCapture();
            _f.Segmenter.Returns(ScriptedSegmenter.Square(30, 30, 4, confidence: 0.5f), ScriptedSegmenter.Square(10, 10, 4, confidence: 0.97f));

            var result = ToolHarness.Run(_f.Highlight(), "{\"part\":\"mug handle\",\"object_id\":\"obj_3\"}");

            Assert.That(result.OutputJson, Is.EqualTo("{\"status\":\"highlighted\",\"part\":\"mug handle\"}"));
            Assert.That(_f.Camera.Captures, Is.EqualTo(1));
            Assert.That(_f.Presenter.Shown.Single().objects.Single().Confidence, Is.EqualTo(0.97f));
            Assert.That(_f.Character.LastLook, Is.Not.Null);
        }

        [Test]
        public void HighlightPart_NoFrameOrNoMatch()
        {
            var noFrame = ToolHarness.Run(_f.Highlight(), "{\"part\":\"handle\"}");
            _f.Scene.LastCapture = _f.PreCapture();
            var noMatch = ToolHarness.Run(_f.Highlight(), "{\"part\":\"handle\"}");

            Assert.That(noFrame.OutputJson, Is.EqualTo("{\"error\":\"no_frame\"}"));
            Assert.That(noMatch.OutputJson, Is.EqualTo("{\"error\":\"no_match\"}"));
        }
    }
}
