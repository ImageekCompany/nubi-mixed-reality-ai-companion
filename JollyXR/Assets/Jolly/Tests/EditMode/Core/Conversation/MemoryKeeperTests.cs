using System.IO;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Memory;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class MemoryKeeperTests
    {
        readonly RecordingLog _log = new();
        readonly ScriptedChatModel _model = new();
        string _dir;
        ConversationMemory _memory;
        MemoryKeeper _keeper;

        [SetUp]
        public void SetUp()
        {
            _dir = TempDirectory.Create();
            var personas = new FakePersonaProvider();
            var limits = new MemoryLimits();
            _memory = new ConversationMemory(_dir, personas, limits, _log);
            var scene = new SceneMemory(Path.Combine(_dir, "scene.json"), new FakeSpatialAnchors(), new FakeClock(), limits, _log);
            _keeper = new MemoryKeeper(_memory, scene, _model, personas, _log);
            Sync.Run(_keeper.Load(default));
        }

        [TearDown]
        public void TearDown() => TempDirectory.Delete(_dir);

        [Test]
        public void AfterTurn_BelowTheCaps_OnlySaves()
        {
            _memory.CommitTurn(new[] { ConversationItem.User("hi"), ConversationItem.Assistant("hello") });

            Sync.Run(_keeper.AfterTurn(default));

            Assert.That(_model.Requests, Is.Empty);
            Assert.That(File.Exists(Path.Combine(_dir, "conversation.companion.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "scene.json")), Is.True);
        }

        [Test]
        public void AfterTurn_OverTheCaps_SummarisesOnTheLiveUserRouteThenSaves()
        {
            FillPastCaps();
            _model.Enqueue().Text("The user likes tea.");

            Sync.Run(_keeper.AfterTurn(default));

            Assert.That(_model.Requests[0].Route, Is.EqualTo(ModelRoute.LiveUser));
            Assert.That(_memory.Summary, Is.EqualTo("The user likes tea."));
            Assert.That(File.ReadAllText(Path.Combine(_dir, "conversation.companion.json")), Does.Contain("likes tea"));
        }

        [Test]
        public void SummaryFailure_IsLoggedAndMemoryIsStillSaved()
        {
            FillPastCaps();
            _model.Enqueue().Fail(ProviderErrorKind.Timeout);

            Sync.Run(_keeper.AfterTurn(default));

            Assert.That(_log.Contains(LogLevel.Warning, "evt=memory.summarize_failed kind=Timeout"), Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "conversation.companion.json")), Is.True);
        }

        [Test]
        public void SaveFailure_IsLoggedNotThrown()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "conversation.companion.json"));

            Sync.Run(_keeper.Save(default));

            Assert.That(_log.Contains(LogLevel.Error, "evt=memory.save_failed"), Is.True);
        }

        void FillPastCaps()
        {
            for (var i = 0; i < 9; i++)
                _memory.CommitTurn(new[] { ConversationItem.User("question " + i), ConversationItem.Assistant("answer " + i) });
            Assert.That(_memory.NeedsSummarization, Is.True);
        }
    }
}
