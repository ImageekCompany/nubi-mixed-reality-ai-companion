using System.Linq;
using System.Text;
using System.Threading;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Memory;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Providers.MetaApi.Common;
using Jolly.Testing;
using Jolly.Tests.EditMode.Core;
using Jolly.Tests.EditMode.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Chat
{
    /// <summary>
    /// The rolling summary over the real Responses model and recorded streams (synthetic conversation, contributor model,
    /// 2026-11-01): the reasoning tokens share max_output_tokens with the text (architecture §8).
    /// </summary>
    public sealed class SummaryStreamTests
    {
        const string Previous = "Sam likes jazz.";

        FakeHttpTransport _transport;
        RecordingLog _log;
        ConversationMemory _memory;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _log = new RecordingLog();
            var personas = new FakePersonaProvider(FakePersonaProvider.Profile("companion"));
            _memory = new ConversationMemory("unused", personas, new MemoryLimits(), _log);
            Sync.Run(_memory.Load("companion", Previous, CancellationToken.None));
            for (var i = 1; i <= 3; i++)
                _memory.CommitTurn(new[] { ConversationItem.User("question " + i), ConversationItem.Assistant("answer " + i) });
        }

        [Test]
        public void RecordedSummary_ReplacesTheSummaryAndFoldsTheOldestTurns()
        {
            _transport.Respond(SseFixtures.Parse("responses_summary"));

            Sync.Run(_memory.Summarize(Model(), CancellationToken.None));

            Assert.That(_memory.Summary, Does.StartWith("User is called Sam.").And.Contain("Ana loves chamomile tea."));
            Assert.That(_memory.Recent.First().Text, Is.EqualTo("question 3"));
            var body = JObject.Parse(Encoding.UTF8.GetString(_transport.Requests.Single().Body));
            Assert.That((int)body["max_output_tokens"], Is.EqualTo(1024));
            Assert.That((string)body["prompt_cache_key"], Does.Match("^jolly-[0-9a-f]{16}$"));
            Assert.That(body["tools"], Is.Null);
        }

        [Test]
        public void RecordedCutOff_ReasoningAteTheBudget_KeepsTheOldSummaryAndItems()
        {
            _transport.Respond(SseFixtures.Parse("responses_incomplete"));

            Sync.Run(_memory.Summarize(Model(), CancellationToken.None));

            Assert.That(_memory.Summary, Is.EqualTo(Previous));
            Assert.That(_memory.Recent.Count, Is.EqualTo(6));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=memory.summary_rejected finish=Length chars=0"), Is.True);
        }

        ResponsesChatModel Model()
        {
            var clock = new FakeClock();
            var retry = new RetryExecutor(clock, new FakeRandom(), _log);
            return new ResponsesChatModel(new MetaApiEndpoint(new InMemorySecrets().With(SecretKeys.MetaApiKey, "fake-key")),
                new MetaSseClient(_transport, retry, clock), clock, new RecordingUsageMeter(),
                ResponsesChatOptions.Background("muse-spark-1.3", "muse-spark-1.3-contributor", 0.01m), _log);
        }
    }
}
