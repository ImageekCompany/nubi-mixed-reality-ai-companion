using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Memory;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    // Same-named fakes from other WPs also live in Jolly.Testing; these tests are written against this WP's.
    using FakePersonaProvider = Jolly.Testing.Conversation.FakePersonaProvider;
    using FakeSpatialAnchors = Jolly.Testing.Conversation.FakeSpatialAnchors;

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
        public void LoadFailure_IsLoggedAndTheSceneStillLoads()
        {
            var anchors = new FakeSpatialAnchors();
            var path = Path.Combine(_dir, "scene.json");
            var saved = new SceneMemory(path, anchors, new FakeClock(), new MemoryLimits(), _log);
            var anchorId = anchors.CreateAndSave(default, default).GetAwaiter().GetResult();
            saved.RememberPlace(new PlaceRecord { Name = "desk", AnchorId = anchorId });
            Sync.Run(saved.Save(default));
            var scene = new SceneMemory(path, anchors, new FakeClock(), new MemoryLimits(), _log);
            var keeper = new MemoryKeeper(new UnreadableConversationMemory(), scene, _model, new FakePersonaProvider(), _log);

            Sync.Run(keeper.Load(default));

            Assert.That(_log.Contains(LogLevel.Error, "evt=memory.load_failed store=conversation"), Is.True);
            Assert.That(scene.TryGetPlace("desk", out _), Is.True);
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

        sealed class UnreadableConversationMemory : IConversationMemory
        {
            public string Summary => string.Empty;
            public IReadOnlyList<ConversationItem> Recent { get; } = new List<ConversationItem>();
            public bool NeedsSummarization => false;
            public void CommitTurn(IReadOnlyList<ConversationItem> items) { }
            public UniTask Summarize(IChatModel model, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Load(string personaId, string carrySummary, CancellationToken ct) => throw new IOException("unreadable");
            public UniTask Save(CancellationToken ct) => UniTask.CompletedTask;
            public void Clear() { }
        }
    }
}
