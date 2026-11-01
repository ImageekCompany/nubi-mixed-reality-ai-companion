using System;
using System.IO;
using System.Linq;
using System.Threading;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Memory;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Memory
{
    public sealed class ConversationMemoryTests
    {
        static readonly CancellationToken None = CancellationToken.None;
        string _dir;
        FakePersonaProvider _personas;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _dir = TempDirectory.Create();
            _personas = new FakePersonaProvider(FakePersonaProvider.Profile("companion"),
                FakePersonaProvider.Profile("psychologist", persistMemory: false));
            _log = new RecordingLog();
        }

        [TearDown]
        public void TearDown() => TempDirectory.Delete(_dir);

        ConversationMemory Create(MemoryLimits limits = null)
        {
            var memory = new ConversationMemory(_dir, _personas, limits ?? new MemoryLimits(), _log);
            Sync.Run(memory.Load(_personas.Current.Id, null, None));
            return memory;
        }

        static ConversationItem[] Turn(int n) => new[]
        {
            ConversationItem.User("question " + n),
            ConversationItem.Call("c" + n, "look_at", "{\"target\":\"user\"}"),
            ConversationItem.Result("c" + n, "{\"status\":\"looking\"}"),
            ConversationItem.Assistant("answer " + n),
        };

        [Test]
        public void CommitTurn_AppendsInOrder()
        {
            var memory = Create();

            memory.CommitTurn(Turn(1));

            Assert.That(memory.Recent.Select(i => i.Kind), Is.EqualTo(new[]
                { ItemKind.UserText, ItemKind.ToolCall, ItemKind.ToolResult, ItemKind.AssistantText }));
        }

        [Test]
        public void CommitTurn_RejectsOpaqueAndImages()
        {
            var memory = Create();

            Assert.Throws<ArgumentException>(() => memory.CommitTurn(new[] { ConversationItem.Opaque("{}") }));
            Assert.Throws<ArgumentException>(() => memory.CommitTurn(new[] { ConversationItem.UserImage(null) }));
            Assert.That(memory.Recent, Is.Empty);
        }

        [Test]
        public void CommitTurn_OrphanCallOrResult_ThrowsAndCommitsNothing()
        {
            var memory = Create();

            Assert.Throws<InvalidOperationException>(() => memory.CommitTurn(new[]
                { ConversationItem.User("a"), ConversationItem.Call("c1", "stop", "{}") }));
            Assert.Throws<InvalidOperationException>(() => memory.CommitTurn(new[] { ConversationItem.Result("c9", "{}") }));
            Assert.That(memory.Recent, Is.Empty);
        }

        [Test]
        public void NeedsSummarization_AfterItemOrCharCap()
        {
            var byCount = Create(new MemoryLimits { RecentItems = 4 });
            byCount.CommitTurn(Turn(1));
            var atCap = byCount.NeedsSummarization;
            byCount.CommitTurn(new[] { ConversationItem.User("x") });

            var byChars = Create(new MemoryLimits { RecentChars = 10 });
            byChars.CommitTurn(new[] { ConversationItem.User(new string('a', 11)) });

            Assert.That(atCap, Is.False);
            Assert.That(byCount.NeedsSummarization, Is.True);
            Assert.That(byChars.NeedsSummarization, Is.True);
        }

        [Test]
        public void Summarize_FoldsOldestHalfAtATurnBoundaryWithOneLiveUserCall()
        {
            var memory = Create();
            for (var i = 1; i <= 5; i++)
                memory.CommitTurn(Turn(i));
            var model = new ScriptedChatModel();
            model.Enqueue().Text("User asked five ").Text("questions.");

            Sync.Run(memory.Summarize(model, None));

            var request = model.Requests.Single();
            Assert.That(request.Route, Is.EqualTo(ModelRoute.LiveUser));
            Assert.That(request.Effort, Is.EqualTo(ReasoningEffort.Minimal));
            Assert.That(request.MaxOutputTokens, Is.EqualTo(1024), "reasoning tokens count against the cap");
            Assert.That(request.Instructions, Does.EndWith("At most 150 words."));
            Assert.That(request.Tools, Is.Empty);
            Assert.That(memory.Summary, Is.EqualTo("User asked five questions."));
            Assert.That(memory.Recent.Count, Is.EqualTo(8), "turns 1-3 folded (cut at the first turn start >= half)");
            Assert.That(memory.Recent[0].Text, Is.EqualTo("question 4"));
            Assert.That(model.Inputs[0][0].Text, Does.Contain("question 1").And.Contain("answer 3").And.Not.Contain("question 4"));
        }

        [Test]
        public void Summarize_WithoutText_KeepsSummaryAndItems()
        {
            var memory = Create();
            for (var i = 1; i <= 5; i++)
                memory.CommitTurn(Turn(i));
            Sync.Run(memory.Summarize(Scripted("Earlier: five questions."), None));
            var model = new ScriptedChatModel();
            model.Enqueue();
            for (var i = 6; i <= 8; i++)
                memory.CommitTurn(Turn(i));
            var before = memory.Recent.Count;

            Sync.Run(memory.Summarize(model, None));

            Assert.That(memory.Summary, Is.EqualTo("Earlier: five questions."));
            Assert.That(memory.Recent.Count, Is.EqualTo(before));
        }

        [Test]
        public void Summarize_CapsSummaryLength()
        {
            var memory = Create(new MemoryLimits { SummaryChars = 20 });
            memory.CommitTurn(Turn(1));
            memory.CommitTurn(Turn(2));
            var model = new ScriptedChatModel();
            model.Enqueue().Text("This summary is far longer than twenty characters.");

            Sync.Run(memory.Summarize(model, None));

            Assert.That(memory.Summary.Length, Is.LessThanOrEqualTo(20));
        }

        [TestCase(FinishReason.Length, "Sam likes jazz and his sister")]
        [TestCase(FinishReason.Incomplete, "Sam likes")]
        [TestCase(FinishReason.Stop, " ")]
        public void Summarize_CutOffOrEmptyReply_KeepsTheOldSummaryAndItems(FinishReason finish, string text)
        {
            var memory = Create();
            Sync.Run(memory.Load("companion", "Sam likes jazz.", None));
            memory.CommitTurn(Turn(1));
            memory.CommitTurn(Turn(2));
            var model = new ScriptedChatModel();
            model.Enqueue().Text(text).Finish(finish);

            Sync.Run(memory.Summarize(model, None));

            Assert.That(memory.Summary, Is.EqualTo("Sam likes jazz."));
            Assert.That(memory.Recent.Count, Is.EqualTo(8));
            Assert.That(_log.Contains(LogLevel.Warning, $"evt=memory.summary_rejected finish={finish}"), Is.True);
        }

        [Test]
        public void Summarize_KeepsItemsCommittedDuringTheCall()
        {
            var memory = Create();
            memory.CommitTurn(Turn(1));
            memory.CommitTurn(Turn(2));
            var model = new ScriptedChatModel();
            var round = model.Enqueue().Gate().Text("s");

            var task = memory.Summarize(model, None);
            memory.CommitTurn(Turn(3));
            round.Release();
            Sync.Run(task);

            Assert.That(memory.Recent.Select(i => i.Text).Where(t => t != null),
                Is.EqualTo(new[] { "question 2", "answer 2", "question 3", "answer 3" }));
        }

        [Test]
        public void Summarize_AfterClearDuringTheCall_ChangesNothing()
        {
            var memory = Create();
            memory.CommitTurn(Turn(1));
            memory.CommitTurn(Turn(2));
            var model = new ScriptedChatModel();
            var round = model.Enqueue().Gate().Text("stale");

            var task = memory.Summarize(model, None);
            memory.Clear();
            round.Release();
            Sync.Run(task);

            Assert.That(memory.Summary, Is.Empty);
            Assert.That(memory.Recent, Is.Empty);
        }

        [Test]
        public void SaveAndLoad_RoundTripsSummaryAndItems()
        {
            var memory = Create();
            memory.CommitTurn(Turn(1));
            memory.CommitTurn(new[] { ConversationItem.User("quote \" and\nnewline") });
            Sync.Run(memory.Summarize(Scripted("Earlier: one question."), None));
            memory.CommitTurn(Turn(2));
            Sync.Run(memory.Save(None));

            var reloaded = Create();

            Assert.That(reloaded.Summary, Is.EqualTo(memory.Summary));
            Assert.That(reloaded.Recent.Count, Is.EqualTo(memory.Recent.Count));
            for (var i = 0; i < memory.Recent.Count; i++)
            {
                Assert.That(reloaded.Recent[i].Kind, Is.EqualTo(memory.Recent[i].Kind));
                Assert.That(reloaded.Recent[i].Text, Is.EqualTo(memory.Recent[i].Text));
                Assert.That(reloaded.Recent[i].CallId, Is.EqualTo(memory.Recent[i].CallId));
                Assert.That(reloaded.Recent[i].ToolName, Is.EqualTo(memory.Recent[i].ToolName));
                Assert.That(reloaded.Recent[i].Json, Is.EqualTo(memory.Recent[i].Json));
            }
            Assert.That(File.Exists(Path.Combine(_dir, "conversation.companion.json")), Is.True);
        }

        [Test]
        public void Load_WithoutFile_UsesCarriedSummary()
        {
            var memory = new ConversationMemory(_dir, _personas, new MemoryLimits(), _log);

            Sync.Run(memory.Load("companion", "carried over", None));

            Assert.That(memory.Summary, Is.EqualTo("carried over"));
            Assert.That(memory.Recent, Is.Empty);
        }

        [Test]
        public void NonPersistingPersona_NeverWritesOrReads()
        {
            Sync.Run(_personas.ApplySwitch("psychologist", None));
            File.WriteAllText(Path.Combine(_dir, "conversation.psychologist.json"), "{\"version\":1,\"summary\":\"leak\",\"recent\":[]}");
            var memory = Create();
            memory.CommitTurn(Turn(1));

            Sync.Run(memory.Save(None));

            Assert.That(memory.Summary, Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(_dir, "conversation.psychologist.json")), Does.Contain("leak"));
        }

        [Test]
        public void Clear_ResetsAndDeletesTheFile()
        {
            var memory = Create();
            memory.CommitTurn(Turn(1));
            Sync.Run(memory.Save(None));

            memory.Clear();

            Assert.That(memory.Recent, Is.Empty);
            Assert.That(File.Exists(Path.Combine(_dir, "conversation.companion.json")), Is.False);
        }

        [Test]
        public void Clear_DeletesEveryPersonasFile_SoNoneIsCarriedBackOnASwitch()
        {
            var memory = Create();
            memory.CommitTurn(Turn(1));
            Sync.Run(memory.Save(None));
            File.WriteAllText(Path.Combine(_dir, "conversation.mentor.json"), "{}");
            File.WriteAllText(Path.Combine(_dir, "scene.json"), "{}");

            memory.Clear();

            Assert.That(Directory.GetFiles(_dir, "conversation.*"), Is.Empty);
            Assert.That(File.Exists(Path.Combine(_dir, "scene.json")), Is.True, "the scene store clears its own file");
        }

        [Test]
        public void Load_CorruptFile_StartsEmptyAndWarns()
        {
            File.WriteAllText(Path.Combine(_dir, "conversation.companion.json"), "{ not json");

            var memory = Create();

            Assert.That(memory.Recent, Is.Empty);
            Assert.That(_log.Contains(LogLevel.Warning, "evt=memory.load_failed"), Is.True);
        }

        static ScriptedChatModel Scripted(string text)
        {
            var model = new ScriptedChatModel();
            model.Enqueue().Text(text);
            return model;
        }
    }
}
