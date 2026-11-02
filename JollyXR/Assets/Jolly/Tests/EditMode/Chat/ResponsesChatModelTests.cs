using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Providers.MetaApi.Common;
using Jolly.Testing;
using Jolly.Tests.EditMode.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Chat
{
    public sealed class ResponsesChatModelTests
    {
        const string LiveModel = "muse-spark-1.3";
        const string SyntheticModel = "muse-spark-1.3-contributor";

        FakeClock _clock;
        RecordingLog _log;
        RecordingUsageMeter _meter;
        FakeHttpTransport _transport;
        InMemorySecrets _secrets;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _log = new RecordingLog();
            _meter = new RecordingUsageMeter();
            _transport = new FakeHttpTransport();
            _secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "fake-key");
        }

        [Test]
        public void Stream_RecordedWebSearch_LogsTheSearchAndSettlesItWithTheUsage()
        {
            // Arrange
            _transport.Respond(SseFixtures.Parse("responses_web_search"));
            var events = new List<ModelEvent>();

            // Act
            var run = Collect(Model().Stream(Request(), CancellationToken.None), events, CancellationToken.None);

            // Assert
            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(events.Select(e => e.Kind), Does.Contain(ModelEventKind.WebSearchCompleted));
            Assert.That(_meter.Records.Single().WebSearches, Is.EqualTo(1));
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.web_search model=" + LiveModel + " id=ws_call_"), Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "searches=1"), Is.True);
        }

        [Test]
        public void Stream_RecordedTextResponse_YieldsEventsAndSettlesActualUsage()
        {
            _transport.Respond(SseFixtures.Parse("responses_text"));
            var events = new List<ModelEvent>();

            var run = Collect(Model().Stream(Request(), CancellationToken.None), events, CancellationToken.None);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(events.First().Kind, Is.EqualTo(ModelEventKind.Started));
            Assert.That(events.Last().Kind, Is.EqualTo(ModelEventKind.Completed));
            Assert.That(_meter.Reservations, Is.EqualTo(new[] { 0.01m }));
            Assert.That(_meter.Records.Single().InputTokens, Is.EqualTo(36));
            Assert.That(_meter.Records.Single().Model, Is.EqualTo(LiveModel));
            Assert.That(_meter.Estimates, Is.Empty);
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_transport.Ended, Is.EqualTo(1));
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.completed"), Is.True);
        }

        [Test]
        public void Stream_SendsAnAuthenticatedResponsesRequestForTheRoutedModel()
        {
            _transport.Respond(SseFixtures.Parse("responses_text"));

            var run = Collect(Model().Stream(Request(ModelRoute.Synthetic), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            var sent = _transport.Requests.Single();
            Assert.That(sent.Url, Is.EqualTo(MetaApiEndpoint.DefaultBaseUrl + "/responses"));
            Assert.That(sent.TryGetHeader("Authorization", out _), Is.True);
            Assert.That((string)JObject.Parse(Encoding.UTF8.GetString(sent.Body))["model"], Is.EqualTo(SyntheticModel));
        }

        [Test]
        public void Stream_ContributorModelConfiguredForLiveUsers_ThrowsBeforeReservingOrSending()
        {
            var run = Collect(Model(liveModel: SyntheticModel).Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.BadRequest));
            Assert.That(_transport.Requests, Is.Empty);
            Assert.That(_meter.Reservations, Is.Empty);
        }

        [Test]
        public void Stream_WithoutApiKey_ThrowsUnauthorizedBeforeReservingOrSending()
        {
            _secrets = new InMemorySecrets();

            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(_transport.Requests, Is.Empty);
            Assert.That(_meter.Reservations, Is.Empty);
        }

        [Test]
        public void Stream_BudgetRefused_ThrowsBudgetExceededWithoutSending()
        {
            _meter.RefuseReservations = true;

            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.BudgetExceeded));
            Assert.That(_transport.Requests, Is.Empty);
        }

        [Test]
        public void Stream_TransientFailureBeforeTheFirstEvent_IsRetried()
        {
            _transport.Fail(new HttpFailureException(503, "")).Respond(SseFixtures.Parse("responses_text"));
            var events = new List<ModelEvent>();

            var run = Collect(Model().Stream(Request(), CancellationToken.None), events, CancellationToken.None);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
            Assert.That(events.Count(e => e.Kind == ModelEventKind.Started), Is.EqualTo(1));
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void Stream_RejectedWithAStatus_IsNotRetriedAndBillsNoInput()
        {
            _transport.Fail(new HttpFailureException(401, "{\"error\":{\"message\":\"Unauthorized\"}}"));

            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates.Single().InputTokens, Is.Zero);
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Warning, "evt=llm.failed"), Is.True);
        }

        [Test]
        public void Stream_FailureAfterTheFirstEvent_IsNotRetried()
        {
            var firstEvents = SseFixtures.Parse("responses_text").Take(8);
            _transport.RespondThenFail(firstEvents, HttpFailureException.Network("connection reset"));
            var events = new List<ModelEvent>();

            var run = Collect(Model().Stream(Request(), CancellationToken.None), events, CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Network));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(events.First().Kind, Is.EqualTo(ModelEventKind.Started));
            Assert.That(events.Any(e => e.Kind == ModelEventKind.TextDelta), Is.True);
            Assert.That(_meter.Estimates.Single().InputTokens, Is.GreaterThan(0));
        }

        [Test]
        public void Stream_Cancelled_AbortsTheRequestAndSettlesAnEstimate()
        {
            _transport.RespondThenHang(SseFixtures.Parse("responses_text").Take(8));
            using var cts = new CancellationTokenSource();
            var events = new List<ModelEvent>();
            var run = Collect(Model().Stream(Request(), cts.Token), events, CancellationToken.None);
            var pending = run.Status;

            cts.Cancel();

            Assert.That(pending, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_transport.Tokens.Single().IsCancellationRequested, Is.True);
            Assert.That(_transport.Ended, Is.EqualTo(1));
            var estimate = _meter.Estimates.Single();
            Assert.That(estimate.Model, Is.EqualTo(LiveModel));
            Assert.That(estimate.InputTokens, Is.GreaterThan(0));
            Assert.That(estimate.OutputTokens, Is.EqualTo(Text(events).Length / 4));
            Assert.That(_meter.Records, Is.Empty);
        }

        [Test]
        public void Stream_ConsumerStopsEarly_AbortsTheRequestAndSettlesAnEstimate()
        {
            _transport.RespondThenHang(SseFixtures.Parse("responses_text").Take(8));

            var run = TakeFirst(Model().Stream(Request(), CancellationToken.None));

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.Ended, Is.EqualTo(1));
            Assert.That(_meter.Estimates, Has.Count.EqualTo(1));
            Assert.That(_meter.OpenReservations, Is.Zero);
        }

        [Test]
        public void Stream_NoFirstEventWithinTheTimeout_RetriesThenFailsWithTimeout()
        {
            _transport.Hang().Hang();
            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(6));
            var afterFirstTimeout = _transport.Requests.Count;
            _clock.Advance(TimeSpan.FromSeconds(6));

            Assert.That(afterFirstTimeout, Is.EqualTo(2));
            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_transport.Tokens.All(t => t.IsCancellationRequested), Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void Stream_SilentAfterTheFirstEvent_FailsWithTimeoutWithoutRetrying()
        {
            _transport.RespondThenHang(SseFixtures.Parse("responses_text").Take(3));
            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(9.9));
            var early = run.Status;
            _clock.Advance(TimeSpan.FromSeconds(0.2));

            Assert.That(early, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void Stream_ResponseFailedMidStream_SurfacesProviderExceptionAndSettles()
        {
            _transport.Respond(SseFixtures.Parse("responses_failed"));
            var events = new List<ModelEvent>();

            var run = Collect(Model().Stream(Request(), CancellationToken.None), events, CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Server));
            Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[] { ModelEventKind.Started, ModelEventKind.TextDelta }));
            Assert.That(_meter.Estimates, Has.Count.EqualTo(1));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void Stream_TimedOutAttemptThenRejected_StillBillsTheTimedOutAttemptsInput()
        {
            _transport.Hang().Fail(new HttpFailureException(503, ""));
            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(6));

            Assert.That(Failure(run).HttpStatus, Is.EqualTo(503));
            Assert.That(_meter.Estimates.Single().InputTokens, Is.EqualTo(InputTokens()));
        }

        [Test]
        public void Stream_EveryAttemptTimedOut_BillsTheInputOncePerAttempt()
        {
            _transport.Hang().Hang();
            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(6));
            _clock.Advance(TimeSpan.FromSeconds(6));

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_meter.Estimates.Single().InputTokens, Is.EqualTo(2 * InputTokens()));
        }

        [Test]
        public void Stream_SpentChangedSubscriberThrows_SettlesTheReservationOnlyOnce()
        {
            _meter.SpentChanged += _ => throw new InvalidOperationException("subscriber");
            _transport.Respond(SseFixtures.Parse("responses_text"));

            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => run.GetAwaiter().GetResult());
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates, Is.Empty);
        }

        [Test]
        public void Stream_Completed_ReadsTheStreamToItsEndInsteadOfAborting()
        {
            _transport.Respond(SseFixtures.Parse("responses_text"));

            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.ReadToEnd, Is.EqualTo(1));
        }

        [Test]
        public void Stream_SilentAfterCompleted_SucceedsOnceTheIdleTimeoutAborts()
        {
            _transport.RespondThenHang(SseFixtures.Parse("responses_text").Where(e => !e.IsDone));
            var events = new List<ModelEvent>();
            var run = Collect(Model().Stream(Request(), CancellationToken.None), events, CancellationToken.None);
            var completedWhilePending = events.Last().Kind == ModelEventKind.Completed && run.Status == UniTaskStatus.Pending;

            _clock.Advance(TimeSpan.FromSeconds(10.1));

            Assert.That(completedWhilePending, Is.True);
            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates, Is.Empty);
            Assert.That(_transport.Ended, Is.EqualTo(1));
        }

        [TestCase(true, 15)]
        [TestCase(false, 10)]
        public void Stream_SilentMidStream_TimesOutAfterTheIdleWindowOfItsRequest(bool webSearch, int idleSeconds)
        {
            // Arrange: a search request may sit silent while the server searches (ADR-0048).
            _transport.RespondThenHang(SseFixtures.Parse("responses_text").Take(3));
            var request = Request();
            request.WebSearch = webSearch ? new WebSearchOptions() : null;
            var run = Collect(Model(searchIdleSeconds: 15).Stream(request, CancellationToken.None), new List<ModelEvent>(),
                CancellationToken.None);

            // Act
            _clock.Advance(TimeSpan.FromSeconds(idleSeconds - 0.5));
            var pendingBefore = run.Status == UniTaskStatus.Pending;
            _clock.Advance(TimeSpan.FromSeconds(0.6));

            // Assert
            Assert.That(pendingBefore, Is.True);
            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
        }

        [Test]
        public void Stream_EndsWithoutCompletion_ThrowsProtocol()
        {
            _transport.Respond(SseFixtures.Parse("responses_text").Take(5));

            var run = Collect(Model().Stream(Request(), CancellationToken.None), new List<ModelEvent>(), CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        }

        ResponsesChatModel Model(string liveModel = LiveModel, int searchIdleSeconds = 0)
        {
            var retry = new RetryExecutor(_clock, new FakeRandom().EnqueueFloat(0f, 0f, 0f), _log);
            return new ResponsesChatModel(new MetaApiEndpoint(_secrets), new MetaSseClient(_transport, retry, _clock), _clock, _meter,
                new ResponsesChatOptions(liveModel, SyntheticModel, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10), 2, 0.01m,
                    searchIdleTimeout: TimeSpan.FromSeconds(searchIdleSeconds)), _log);
        }

        static ModelRequest Request(ModelRoute route = ModelRoute.LiveUser) => new()
        {
            Route = route,
            Instructions = "You are Nubi.",
            Input = new[] { ConversationItem.User("Say hello to me in one short sentence.") },
        };

        static long InputTokens() => new ResponsesRequestWriter().Write(Request(), LiveModel).InputChars / 4;

        static async UniTask Collect(IUniTaskAsyncEnumerable<ModelEvent> stream, List<ModelEvent> into, CancellationToken ct)
        {
            await foreach (var e in stream.WithCancellation(ct))
                into.Add(e);
        }

        static async UniTask TakeFirst(IUniTaskAsyncEnumerable<ModelEvent> stream)
        {
            await foreach (var _ in stream)
                break;
        }

        static ProviderException Failure(UniTask run) =>
            Assert.Throws<ProviderException>(() => run.GetAwaiter().GetResult());

        static string Text(IEnumerable<ModelEvent> events) =>
            string.Concat(events.Where(e => e.Kind == ModelEventKind.TextDelta).Select(e => e.Text));
    }
}
