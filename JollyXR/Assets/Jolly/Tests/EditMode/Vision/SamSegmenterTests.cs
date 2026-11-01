using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Vision;
using Jolly.Testing;
using Jolly.Tests.EditMode.Vision.Doubles;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class SamSegmenterTests
    {
        const string Model = "sam-3.1";
        static readonly SamOptions Options = new(Model, 0.0025m, TimeSpan.FromSeconds(5));

        FakeClock _clock;
        RecordingUsageMeter _usage;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _usage = new RecordingUsageMeter();
            _log = new RecordingLog();
        }

        [UnityTest]
        public IEnumerator Segment_ConceptPrompt_PostsOneBitRequestWithInlineImage() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(VisionFixtures.Sam(VisionFixtures.PenConcept));

            await Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            Assert.That(http.LastPath, Is.EqualTo("responses"));
            var body = JObject.Parse(http.LastBody);
            Assert.That((string)body["model"], Is.EqualTo(Model));
            Assert.That((bool)body["stream"], Is.False);
            Assert.That((bool)body["store"], Is.False);
            Assert.That((string)body["metadata"]["mask_encoding"], Is.EqualTo("one_bit"));
            Assert.That((string)body["metadata"]["include_confidence"], Is.EqualTo("true"));
            var content = body["input"][0]["content"];
            Assert.That((string)body["input"][0]["role"], Is.EqualTo("user"));
            Assert.That((string)content[0]["type"], Is.EqualTo("input_text"));
            Assert.That((string)content[0]["text"], Is.EqualTo("pen"));
            Assert.That((string)content[1]["type"], Is.EqualTo("input_image"));
            Assert.That((string)content[1]["image_url"], Is.EqualTo("data:image/jpeg;base64,/9j/2Q=="));
        });

        [UnityTest]
        public IEnumerator Segment_PointPrompt_SendsTokensSizedToTheImage() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(VisionFixtures.Sam(VisionFixtures.MugConcept));
            var prompt = SegmentPrompt.ForPoints(new[] { new PromptPoint(400, 180), new PromptPoint(10, 300, false) });

            await Segmenter(http).Segment(VisionFixtures.Jpeg(640, 360), prompt, CancellationToken.None);

            Assert.That((string)JObject.Parse(http.LastBody)["input"][0]["content"][0]["text"], Is.EqualTo(
                "<0f>1<|point;x=400;y=180;w=640;h=360|>-<|point;x=10;y=300;w=640;h=360|>"));
        });

        [UnityTest]
        public IEnumerator Segment_PenFixture_ReturnsBoxLocalDecodedMaskOnTheMainThread() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(VisionFixtures.Sam(VisionFixtures.PenConcept));

            var objects = await Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            Assert.That(PlayerLoopHelper.IsMainThread, Is.True);
            Assert.That(objects.Count, Is.EqualTo(1));
            var pen = objects[0];
            Assert.That(pen.Id, Is.EqualTo(0));
            Assert.That(pen.Box, Is.EqualTo(new RectInt(50, 26, 323, 372)));
            Assert.That(pen.Confidence, Is.EqualTo(0.871f).Within(0.001f));
            Assert.That((pen.Mask.Width, pen.Mask.Height), Is.EqualTo((pen.Box.width, pen.Box.height)));
            Assert.That(pen.Mask[pen.Mask.Width / 2, pen.Mask.Height / 2], Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=sam.done"), Is.True);
        });

        [UnityTest]
        public IEnumerator Segment_SettlesTheReservationWithTheBilledImage() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(VisionFixtures.Sam(VisionFixtures.PenConcept));

            await Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            Assert.That(_usage.Reserved, Is.EqualTo(new[] { 0.0025m }));
            Assert.That(_usage.Recorded, Has.Count.EqualTo(1));
            Assert.That(_usage.Recorded[0].usage.Model, Is.EqualTo(Model));
            Assert.That(_usage.Recorded[0].usage.Images, Is.EqualTo(1));
            Assert.That(_usage.OpenReservations, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_NoMatch_IsAnEmptyListAndStillBilled() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(VisionFixtures.Sam(VisionFixtures.MugNoMatch));

            var objects = await Segmenter(http).Segment(VisionFixtures.Jpeg(1280, 720), SegmentPrompt.ForText("banana"), CancellationToken.None);

            Assert.That(objects, Is.Empty);
            Assert.That(_usage.Recorded[0].usage.Images, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Segment_BudgetRefused_ThrowsBudgetExceededWithoutCalling() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(VisionFixtures.Sam(VisionFixtures.PenConcept));
            _usage.AllowReservations = false;

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.BudgetExceeded));
            Assert.That(http.Calls, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_LosslessMask_IsAProtocolError() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(Response("completed", VisionFixtures.Sam(VisionFixtures.PenPointBoxLossless)));

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
            Assert.That(e.RequestId, Is.EqualTo("resp_test"));
            Assert.That(_usage.OpenReservations, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_FailedResponse_IsAServerError() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning(Response("failed", ""));

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Server));
        });

        [UnityTest]
        public IEnumerator Segment_UnparsableBody_IsAProtocolError() => UniTask.ToCoroutine(async () =>
        {
            var http = FakeMetaApiJsonClient.Returning("<html>bad gateway</html>");

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        });

        [UnityTest]
        public IEnumerator Segment_TransportFailure_PropagatesAndReleasesTheReservation() => UniTask.ToCoroutine(async () =>
        {
            var failure = new ProviderException("meta", ProviderErrorKind.RateLimited, "429", 429);
            var http = FakeMetaApiJsonClient.Throwing(failure);

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter(http).Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e, Is.SameAs(failure));
            Assert.That(_usage.Estimated, Has.Count.EqualTo(1));
            Assert.That(_usage.OpenReservations, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_NoAnswerWithinTheTimeout_ThrowsTimeout() => UniTask.ToCoroutine(async () =>
        {
            var task = Segmenter(FakeMetaApiJsonClient.Hanging())
                .Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(4.999));
            var before = task.Status;
            _clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.That(before, Is.EqualTo(UniTaskStatus.Pending));
            var e = await AsyncAssert.Throws<ProviderException>(() => task);
            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_usage.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_CallerCancels_ThrowsOperationCanceledNotTimeout() => UniTask.ToCoroutine(async () =>
        {
            using var cts = new CancellationTokenSource();
            var task = Segmenter(FakeMetaApiJsonClient.Hanging())
                .Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), cts.Token);

            cts.Cancel();

            await AsyncAssert.Throws<OperationCanceledException>(() => task);
            Assert.That(_usage.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        SamSegmenter Segmenter(IMetaApiJsonClient http) => new(http, _usage, _clock, _log, Options);

        static string Response(string status, string text) => new JObject
        {
            ["id"] = "resp_test",
            ["status"] = status,
            ["error"] = status == "failed" ? new JObject { ["message"] = "backend unavailable" } : null,
            ["output"] = new JArray(new JObject
            {
                ["type"] = "message",
                ["content"] = new JArray(new JObject { ["type"] = "output_text", ["text"] = text }),
            }),
            ["usage"] = new JObject { ["images_processed"] = 1 },
        }.ToString();
    }
}
