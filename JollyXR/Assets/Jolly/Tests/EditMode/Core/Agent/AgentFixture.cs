using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Agent;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Diagnostics;
using Jolly.Core.Memory;
using Jolly.Core.Spatial;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;

namespace Jolly.Tests.EditMode.Core.Agent
{
    /// <summary>An AgentLoop over scripted fakes and the real registry and memories (never saved to disk).</summary>
    sealed class AgentFixture
    {
        public readonly ScriptedChatModel Model = new();
        public readonly ToolRegistry Registry = new();
        public readonly FakePersonaProvider Personas = new();
        public readonly FakeLocalizer Localizer = new();
        public readonly FakeUserPreferences Preferences = new();
        public readonly FakeClock Clock = new();
        public readonly RecordingLog Log = new();
        public readonly ConversationEvents Events = new();
        public readonly ConversationMemory Memory;
        public readonly SceneMemory Scene;
        public readonly FakeRoomScan RoomScan = new() { Status = RoomScanStatus.Pending };
        public readonly FakeWorldQuery World = new();
        public readonly FakeUserPoseProvider User = new();
        public readonly AgentLoop Loop;
        public readonly AgentSettings Settings = new() { SafetyIdentifier = "0123456789abcdef0123456789abcdef" };
        public string Spoken = "";

        public AgentFixture()
        {
            var limits = new MemoryLimits();
            Memory = new ConversationMemory("unused", Personas, limits, Log);
            Scene = new SceneMemory("unused", new FakeSpatialAnchors(), Clock, limits, Log);
            var room = new RoomContext(RoomScan, World, User, new RoomSettings());
            Loop = new AgentLoop(Model, Registry, Memory, Scene, room, Personas, Localizer, Preferences, Log, Clock,
                new LatencyTracker(Clock, Log), Events, Settings);
        }

        public ScriptedTool Tool(string name, ToolConcurrency concurrency = ToolConcurrency.Parallel, bool manual = false,
                                 TimeSpan? timeout = null)
        {
            var tool = new ScriptedTool(name, concurrency, timeout: timeout) { Manual = manual };
            Registry.Register(tool);
            return tool;
        }

        public StreamCollector<AgentEvent> Run(string text = "hello", CancellationToken ct = default) =>
            StreamCollector<AgentEvent>.Start(Loop.RunTurn(new UserTurn { TurnId = 1, Text = text, LanguageTag = "en" },
                () => Spoken, ct));

        public static string Describe(IEnumerable<AgentEvent> events) => string.Join(" ", events.Select(e => e.Kind switch
        {
            AgentEventKind.SpeechDelta => "say:" + e.Text,
            AgentEventKind.ToolStarted => "start:" + e.ToolName,
            AgentEventKind.ToolCompleted => "done:" + e.ToolName + (e.ToolResult.Ok ? "" : "!"),
            _ => e.Kind.ToString(),
        }));

        public string MemoryKinds() => string.Join(",", Memory.Recent.Select(i => i.Kind));

        public static void Completed(StreamCollector<AgentEvent> run) =>
            NUnit.Framework.Assert.That(run.Status, NUnit.Framework.Is.EqualTo(UniTaskStatus.Succeeded), run.Error?.ToString());
    }
}
