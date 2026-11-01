using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Providers.MetaApi.Common;
using Jolly.Testing;
using Jolly.Tests.EditMode.Net;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Agent
{
    /// <summary>
    /// The device failure of 2026-11-01: Responses sent no first event within the timeout, twice. The loop must signal
    /// Delayed (a hold line) while it waits, and the real adapter's retry must either recover or end in Failed.
    /// </summary>
    public sealed class SlowModelTests
    {
        [Test]
        public void FirstEventLate_SignalsDelayedOnceThenSpeaks()
        {
            var f = new AgentFixture();
            var round = f.Model.Enqueue().DelayStart().Text("Here I am.");

            var run = f.Run();
            f.Clock.Advance(TimeSpan.FromSeconds(1.9));
            var early = AgentFixture.Describe(run.Items);
            f.Clock.Advance(TimeSpan.FromSeconds(0.1));
            var atHold = AgentFixture.Describe(run.Items);
            round.Release();

            Assert.That(early, Is.Empty, "the usual 0.3–1.4 s wait gets no hold line");
            Assert.That(atHold, Is.EqualTo("Delayed"));
            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items), Is.EqualTo("Delayed say:Here I am. Completed"));
            Assert.That(f.Clock.PendingDelays, Is.Zero, "the hold timer stops at the first event");
            Assert.That(f.Log.Contains(LogLevel.Info, "evt=agent.hold_line turn=1 round=0 line=1"), Is.True);
        }

        [Test]
        public void FirstEventInTime_NeverSignalsDelayed()
        {
            var f = new AgentFixture();
            f.Model.Enqueue().Text("Hi.");

            var run = f.Run();
            f.Clock.Advance(TimeSpan.FromSeconds(30));

            AgentFixture.Completed(run);
            Assert.That(AgentFixture.Describe(run.Items), Is.EqualTo("say:Hi. Completed"));
        }

        [Test]
        public void HoldLineOff_NeverSignalsDelayed()
        {
            var f = new AgentFixture();
            f.Settings.HoldLineAfter = TimeSpan.Zero;
            var round = f.Model.Enqueue().DelayStart().Text("Hi.");

            var run = f.Run();
            f.Clock.Advance(TimeSpan.FromSeconds(10));
            round.Release();

            Assert.That(AgentFixture.Describe(run.Items), Is.EqualTo("say:Hi. Completed"));
        }

        [Test]
        public void FirstAttemptTimesOut_HoldLineThenTheRetryAnswers()
        {
            var transport = new FakeHttpTransport().Hang().Respond(SseFixtures.Parse("responses_text"));
            var f = new AgentFixture(c => VoiceModel(transport, c));
            var clock = f.Clock;

            var run = f.Run();
            clock.Advance(TimeSpan.FromSeconds(2));
            var atHold = AgentFixture.Describe(run.Items);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.That(atHold, Is.EqualTo("Delayed"));
            AgentFixture.Completed(run);
            Assert.That(transport.Requests, Has.Count.EqualTo(2));
            Assert.That(run.Items.Count(e => e.Kind == AgentEventKind.Delayed), Is.EqualTo(1));
            Assert.That(run.Items.Any(e => e.Kind == AgentEventKind.SpeechDelta), Is.True);
            Assert.That(run.Items.Last().Kind, Is.EqualTo(AgentEventKind.Completed));
            Assert.That(clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void BothAttemptsTimeOut_TwoHoldLinesThenFailedWithin9Seconds()
        {
            var transport = new FakeHttpTransport().Hang().Hang();
            var f = new AgentFixture(c => VoiceModel(transport, c));
            var clock = f.Clock;

            var run = f.Run();
            clock.Advance(TimeSpan.FromSeconds(7.9));
            var beforeSecondTimeout = run.Status;
            clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(beforeSecondTimeout, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(AgentFixture.Describe(run.Items), Is.EqualTo("Delayed Delayed Failed"));
            Assert.That(run.Items.Last().Error.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(transport.Requests, Has.Count.EqualTo(2));
            AgentFixture.Completed(run);
            Assert.That(clock.PendingDelays, Is.Zero);
        }

        /// <summary>The production voice-turn adapter (4 s first event × 2 attempts), with no retry backoff.</summary>
        static IChatModel VoiceModel(FakeHttpTransport transport, FakeClock c)
        {
            var log = new RecordingLog();
            var retry = new RetryExecutor(c, new FakeRandom().EnqueueFloat(0f, 0f, 0f), log);
            var secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "fake-key");
            return new ResponsesChatModel(new MetaApiEndpoint(secrets), new MetaSseClient(transport, retry, c),
                new RecordingUsageMeter(), ResponsesChatOptions.VoiceTurn("muse-spark-1.3", "muse-spark-1.3-contributor", 0.01m),
                log);
        }
    }
}
