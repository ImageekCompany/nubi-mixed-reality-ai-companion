using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.Retry;
using Jolly.Net.Sse;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Providers.MetaApi.Common;
using Jolly.Testing;
using Jolly.Tests.EditMode.Net;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Chat
{
    /// <summary>ADR-0038: the voice route's hedged request, through the real ResponsesChatModel and MetaSseClient.</summary>
    public sealed class FirstEventHedgeTests
    {
        FakeClock _clock;
        RecordingLog _log;
        RecordingUsageMeter _meter;
        FakeHttpTransport _transport;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _log = new RecordingLog();
            _meter = new RecordingUsageMeter();
            _transport = new FakeHttpTransport();
        }

        [Test]
        public void OriginalAnswersBeforeHedgeAfter_SendsNoHedge()
        {
            _transport.RespondAfter(_clock, TimeSpan.FromSeconds(1), Text());
            var run = Run(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(1));
            var status = run.Status;
            _clock.Advance(TimeSpan.FromSeconds(5));

            Assert.That(status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_meter.Reservations, Has.Count.EqualTo(1));
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates, Is.Empty);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge"), Is.False);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.first_event leg=original ttfb_ms=1000 ttfe_ms=1000"), Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void OriginalSilent_HedgeStartsAtHedgeAfterWinsAndAbortsTheOriginal()
        {
            _transport.Hang().RespondAfter(_clock, TimeSpan.FromSeconds(0.5), Text());
            var run = Run(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(2.49));
            var beforeHedge = _transport.Requests.Count;
            _clock.Advance(TimeSpan.FromSeconds(0.01));
            var atHedge = _transport.Requests.Count;
            _clock.Advance(TimeSpan.FromSeconds(0.5));

            Assert.That(beforeHedge, Is.EqualTo(1));
            Assert.That(atHedge, Is.EqualTo(2));
            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.Requests[1].Body, Is.SameAs(_transport.Requests[0].Body));
            Assert.That(_transport.Tokens[0].IsCancellationRequested, Is.True);
            Assert.That(_meter.Reservations, Has.Count.EqualTo(2));
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates.Single().OutputTokens, Is.Zero);
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge status=started elapsed_ms=2500 reason=silent"), Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge status=won_hedge elapsed_ms=3000"), Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.first_event leg=hedge ttfb_ms=500 ttfe_ms=500"), Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void OriginalAnswersAfterTheHedgeStarted_WinsAndAbortsTheHedge()
        {
            _transport.RespondAfter(_clock, TimeSpan.FromSeconds(3), Text()).Hang();
            var run = Run(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(3));

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
            Assert.That(_transport.Tokens[1].IsCancellationRequested, Is.True);
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates, Has.Count.EqualTo(1));
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge status=won_original elapsed_ms=3000"), Is.True);
        }

        [Test]
        public void NeitherAnswersByTheFirstEventDeadline_FailsWithTimeoutAndNoRetry()
        {
            _transport.Hang().Hang();
            var run = Run(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(3.9));
            var beforeDeadline = run.Status;
            _clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(beforeDeadline, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
            Assert.That(_transport.Tokens.All(t => t.IsCancellationRequested), Is.True);
            Assert.That(_meter.Estimates.Select(e => e.InputTokens), Is.EqualTo(new[] { InputTokens(), InputTokens() }));
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge status=both_timeout elapsed_ms=4000 " +
                                                     "original=\"no first stream event after 4 s; no byte\" " +
                                                     "hedge=\"no first stream event after 1.5 s; no byte\""), Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void HedgeSendsAByteButNoEvent_BothTimeoutSaysWhichLegGotAByte()
        {
            _transport.Hang().ByteThenHang();
            var run = Run(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(4));

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_log.Contains(LogLevel.Info, "original=\"no first stream event after 4 s; no byte\" " +
                                                     "hedge=\"no first stream event after 1.5 s; first byte at 0 ms\""), Is.True);
        }

        [Test]
        public void BudgetCoversOneRequest_DoesNotHedge()
        {
            _meter.MaxOpenReservations = 1;
            _transport.Hang();
            var run = Run(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(2.5));
            var atHedge = _transport.Requests.Count;
            _clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.That(atHedge, Is.EqualTo(1));
            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge status=skipped elapsed_ms=2500 reason=budget"), Is.True);
            Assert.That(_log.Contains(LogLevel.Info, "status=started"), Is.False);
            Assert.That(_meter.OpenReservations, Is.Zero);
        }

        [Test]
        public void TurnCancelledWhileBothWait_AbortsBothAndSettlesBoth()
        {
            _transport.Hang().Hang();
            using var cts = new CancellationTokenSource();
            var run = Run(cts.Token);
            _clock.Advance(TimeSpan.FromSeconds(3));

            cts.Cancel();

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_transport.Tokens, Has.Count.EqualTo(2));
            Assert.That(_transport.Tokens.All(t => t.IsCancellationRequested), Is.True);
            Assert.That(_transport.Ended, Is.EqualTo(2));
            Assert.That(_meter.Estimates, Has.Count.EqualTo(2));
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Info, "status=both_"), Is.False);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void TurnCancelledBeforeHedgeAfter_NeverHedges()
        {
            _transport.Hang();
            using var cts = new CancellationTokenSource();
            var run = Run(cts.Token);
            _clock.Advance(TimeSpan.FromSeconds(1));

            cts.Cancel();
            _clock.Advance(TimeSpan.FromSeconds(5));

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void OriginalFailsFastWithARetryableStatus_HedgesAtOnce()
        {
            _transport.Fail(new HttpFailureException(503, "")).Respond(Text());

            var run = Run(CancellationToken.None);

            Assert.That(run.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(_transport.Requests, Has.Count.EqualTo(2));
            Assert.That(_meter.Records, Has.Count.EqualTo(1));
            Assert.That(_meter.Estimates.Single().InputTokens, Is.Zero);
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge status=started elapsed_ms=0 reason=failed"), Is.True);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void OriginalRejected_FailsWithoutHedging()
        {
            _transport.Fail(new HttpFailureException(401, ""));

            var run = Run(CancellationToken.None);

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_meter.OpenReservations, Is.Zero);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void HedgeOff_SilentOriginalIsTheOnlyRequest()
        {
            _transport.Hang();
            var model = Model(new ResponsesChatOptions("muse-spark-1.3", "muse-spark-1.3-contributor",
                TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10), 1, 0.01m));
            var run = Collect(model.Stream(Request(), CancellationToken.None));

            _clock.Advance(TimeSpan.FromSeconds(4));

            Assert.That(Failure(run).Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_log.Contains(LogLevel.Info, "evt=llm.hedge"), Is.False);
        }

        UniTask Run(CancellationToken ct) =>
            Collect(Model(ResponsesChatOptions.VoiceTurn("muse-spark-1.3", "muse-spark-1.3-contributor", 0.01m))
                .Stream(Request(), ct));

        ResponsesChatModel Model(ResponsesChatOptions options)
        {
            var retry = new RetryExecutor(_clock, new FakeRandom(), _log);
            var secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "fake-key");
            return new ResponsesChatModel(new MetaApiEndpoint(secrets), new MetaSseClient(_transport, retry, _clock), _clock,
                _meter, options, _log);
        }

        static IEnumerable<SseEvent> Text() => SseFixtures.Parse("responses_text");

        static ModelRequest Request() => new()
        {
            Route = ModelRoute.LiveUser,
            Instructions = "You are Nubi.",
            Input = new[] { ConversationItem.User("Say hello to me in one short sentence.") },
        };

        static long InputTokens() => new ResponsesRequestWriter().Write(Request(), "muse-spark-1.3").InputChars / 4;

        static async UniTask Collect(IUniTaskAsyncEnumerable<ModelEvent> stream)
        {
            await foreach (var _ in stream)
            {
            }
        }

        static ProviderException Failure(UniTask run) =>
            Assert.Throws<ProviderException>(() => run.GetAwaiter().GetResult());
    }
}
