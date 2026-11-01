using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Common;
using Jolly.Providers.MetaApi.Vision;
using Jolly.Testing;
using Jolly.Tests.EditMode.Net;
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
        static readonly SamOptions Options = new(Model, 0.0025m, TimeSpan.FromSeconds(5), maxAttempts: 1);

        FakeClock _clock;
        RecordingUsageMeter _usage;
        RecordingLog _log;
        FakeHttpTransport _transport;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _usage = new RecordingUsageMeter();
            _log = new RecordingLog();
            _transport = new FakeHttpTransport();
        }

        [UnityTest]
        public IEnumerator Segment_ConceptPrompt_StreamsAnAuthenticatedOneBitRequestWithInlineImage() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.FromRecorded(VisionFixtures.Sam(VisionFixtures.PenConcept)));

            await Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            var sent = _transport.Requests.Single();
            Assert.That(sent.Url, Is.EqualTo(MetaApiEndpoint.DefaultBaseUrl + "/responses"));
            Assert.That(sent.TryGetHeader("Authorization", out _), Is.True);
            var body = JObject.Parse(Encoding.UTF8.GetString(sent.Body));
            Assert.That((string)body["model"], Is.EqualTo(Model));
            Assert.That((bool)body["stream"], Is.True);
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
            _transport.Respond(SamStreams.FromRecorded(VisionFixtures.Sam(VisionFixtures.MugConcept)));
            var prompt = SegmentPrompt.ForPoints(new[] { new PromptPoint(400, 180), new PromptPoint(10, 300, false) });

            await Segmenter().Segment(VisionFixtures.Jpeg(640, 360), prompt, CancellationToken.None);

            Assert.That((string)SentBody()["input"][0]["content"][0]["text"], Is.EqualTo(
                "<0f>1<|point;x=400;y=180;w=640;h=360|>-<|point;x=10;y=300;w=640;h=360|>"));
        });

        [UnityTest]
        public IEnumerator Segment_PenFixture_ReturnsBoxLocalDecodedMaskOnTheMainThread() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.FromRecorded(VisionFixtures.Sam(VisionFixtures.PenConcept)));

            var objects = await Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

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
        public IEnumerator Segment_OutputTextDone_ReturnsWithoutWaitingForCompletedAndAbortsTheRest() => UniTask.ToCoroutine(async () =>
        {
            var text = VisionFixtures.SamOutputText(VisionFixtures.PenConcept);
            _transport.RespondThenHang(SamStreams.UpToOutputTextDone(text));

            var objects = await Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            Assert.That(objects.Count, Is.EqualTo(1));
            Assert.That(_transport.Ended, Is.EqualTo(1));
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
            Assert.That(_usage.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_TextOnlyInDeltas_IsTakenWhenTheResponseCompletes() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.DeltaThenCompleted(VisionFixtures.SamOutputText(VisionFixtures.PenConcept)));

            var objects = await Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            Assert.That(objects.Count, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Segment_SettlesTheReservationWithTheBilledImage() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.FromRecorded(VisionFixtures.Sam(VisionFixtures.PenConcept)));

            await Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            Assert.That(_usage.Reservations, Is.EqualTo(new[] { 0.0025m }));
            var record = _usage.Records.Single();
            Assert.That(record.Model, Is.EqualTo(Model));
            Assert.That(record.Images, Is.EqualTo(1));
            Assert.That(_usage.Estimates, Is.Empty);
            Assert.That(_usage.OpenReservations, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_NoMatch_IsAnEmptyListAndStillBilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.FromRecorded(VisionFixtures.Sam(VisionFixtures.MugNoMatch)));

            var objects = await Segmenter().Segment(VisionFixtures.Jpeg(1280, 720), SegmentPrompt.ForText("banana"), CancellationToken.None);

            Assert.That(objects, Is.Empty);
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Segment_ModelPhraseWithNoUsableCharacters_IsNoMatchWithoutARequest() => UniTask.ToCoroutine(async () =>
        {
            var objects = await Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("???"), CancellationToken.None);

            Assert.That(objects, Is.Empty);
            Assert.That(_transport.Requests, Is.Empty);
            Assert.That(_usage.Reservations, Is.Empty);
        });

        [UnityTest]
        public IEnumerator Segment_BudgetRefused_ThrowsBudgetExceededWithoutCalling() => UniTask.ToCoroutine(async () =>
        {
            _usage.RefuseReservations = true;

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.BudgetExceeded));
            Assert.That(e.Provider, Is.EqualTo(MetaErrors.Provider));
            Assert.That(_transport.Requests, Is.Empty);
        });

        [UnityTest]
        public IEnumerator Segment_LosslessMask_IsAProtocolErrorAndBilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.WithText(VisionFixtures.Sam(VisionFixtures.PenPointBoxLossless)));

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
            Assert.That(e.RequestId, Is.EqualTo("resp_test"));
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
            Assert.That(_usage.OpenReservations, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_FailedResponse_IsMappedFromItsCodeAndAssumedBilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.Failed("server_error"));

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Server));
            Assert.That(e.RequestId, Is.EqualTo("resp_test"));
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator Segment_StreamEndingBeforeOutput_IsAProtocolError() => UniTask.ToCoroutine(async () =>
        {
            _transport.Respond(SamStreams.EndingBeforeOutput());

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
            Assert.That(_usage.OpenReservations, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_RejectedWithAStatus_IsMappedAndSettledAsUnbilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Fail(new HttpFailureException(400, "{\"error\":{\"message\":\"w/h mismatch\"}}"));

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That((e.Kind, e.HttpStatus, e.Provider), Is.EqualTo((ProviderErrorKind.BadRequest, 400, MetaErrors.Provider)));
            var estimate = _usage.Estimates.Single();
            Assert.That((estimate.InputTokens, estimate.OutputTokens), Is.EqualTo((0L, 0L)));
            Assert.That(_usage.Records, Is.Empty);
        });

        [UnityTest]
        public IEnumerator Segment_ServerErrorStatus_IsAssumedBilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Fail(new HttpFailureException(503, "{\"error\":{\"message\":\"overloaded\"}}"));

            var e = await AsyncAssert.Throws<ProviderException>(() =>
                Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None));

            Assert.That(e.HttpStatus, Is.EqualTo(503));
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
            Assert.That(_usage.Estimates, Is.Empty);
        });

        [UnityTest]
        public IEnumerator Segment_NoAnswerWithinTheTimeout_ThrowsTimeoutAndIsAssumedBilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Hang();
            var task = Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(4.999));
            var before = task.Status;
            _clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.That(before, Is.EqualTo(UniTaskStatus.Pending));
            var e = await AsyncAssert.Throws<ProviderException>(() => task);
            Assert.That((e.Kind, e.Provider), Is.EqualTo((ProviderErrorKind.Timeout, MetaErrors.Provider)));
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
            Assert.That(_usage.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        [UnityTest]
        public IEnumerator Segment_CallerCancels_ThrowsOperationCanceledAndIsAssumedBilled() => UniTask.ToCoroutine(async () =>
        {
            _transport.Hang();
            using var cts = new CancellationTokenSource();
            var task = Segmenter().Segment(VisionFixtures.Jpeg(640, 428), SegmentPrompt.ForText("pen"), cts.Token);

            cts.Cancel();

            await AsyncAssert.Throws<OperationCanceledException>(() => task);
            Assert.That(_usage.Records.Single().Images, Is.EqualTo(1));
            Assert.That(_usage.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        });

        SamSegmenter Segmenter()
        {
            var secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "fake-key");
            var client = new MetaSseClient(_transport, new RetryExecutor(_clock, new FakeRandom(), _log), _clock);
            return new SamSegmenter(new MetaApiEndpoint(secrets), client, _usage, _clock, _log, Options);
        }

        JObject SentBody() => JObject.Parse(Encoding.UTF8.GetString(_transport.Requests.Single().Body));
    }
}
