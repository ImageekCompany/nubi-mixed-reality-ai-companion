using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Diagnostics;
using Jolly.Core.Memory;
using Jolly.Core.Tools;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    /// <summary>The real controller, pipeline, state machine and memories over scripted voice, agent and speech fakes.</summary>
    sealed class ConversationFixture : IDisposable
    {
        public readonly FakeClock Clock = new();
        public readonly RecordingLog Log = new();
        public readonly FakeLocalizer Localizer;
        public readonly FakePersonaProvider Personas;
        public readonly FakeVoiceActivation Voice = new();
        public readonly FakeAudioCapture Mic = new();
        public readonly ScriptedSpeechToText Stt = new();
        public readonly FakeCameraFrameSource Camera = new();
        public readonly FakeSpeechOutput Output = new();
        public readonly RecordingSpeechQueue Queue;
        public readonly ScriptedAgentLoop Agent = new();
        public readonly RecordingOfflineResponder Offline = new();
        public readonly RecordingCharacterController Character = new();
        public readonly FakeConnectivityMonitor Connectivity = new();
        public readonly FakeUsageMeter Usage = new();
        public readonly ScriptedChatModel Model = new();
        public readonly ConversationEvents Events = new();
        public readonly ConversationStateMachine Machine;
        public readonly ConversationTimings Timings = new() { AcknowledgeOnRelease = false };
        public readonly ConversationMemory Memory;
        public readonly SceneMemory Scene;
        public readonly ConversationController Controller;
        public readonly List<ConversationState> States = new();
        public readonly List<string> Captions = new();
        public readonly List<string> Partials = new();
        public readonly string Directory = TempDirectory.Create();
        readonly CancellationTokenSource _app = new();
        readonly List<IDisposable> _disposables = new();
        UniTask _running;

        public ConversationFixture(string languageTag = "en", params PersonaProfile[] personas)
        {
            Localizer = new FakeLocalizer(languageTag);
            Personas = new FakePersonaProvider(personas);
            Queue = new RecordingSpeechQueue(Output);
            Machine = new ConversationStateMachine(Log);
            var limits = new MemoryLimits();
            Memory = new ConversationMemory(Directory, Personas, limits, Log);
            Scene = new SceneMemory(Path.Combine(Directory, "scene.json"), new FakeSpatialAnchors(), Clock, limits, Log);

            var latency = new LatencyTracker(Clock, Log);
            var frames = new FrameGrabber(Camera, new FakeImageEncoder(), Clock, new VisionSettings());
            var listener = new TurnListener(Mic, Stt, frames, TranscriptAliases.ForName("Nubi", new[] { "newbie" }), Localizer, Events, latency, Log, Timings);
            var guards = new TurnGuards(new StopGuard(Localizer), new CrisisGuard(Localizer), Character);
            var speech = new TurnSpeech(Queue, new FakeSentenceSplitter(), Output, Machine, Events, latency, Clock);
            var pipeline = new TurnPipeline(listener, guards, speech, Agent, Offline, Personas, Machine, Events, latency,
                Localizer, Log, Timings);
            var switcher = new PersonaSwitcher(Personas, Personas, Memory, Character, Log);
            var keeper = new MemoryKeeper(Memory, Scene, Model, Personas, Log);
            Controller = new ConversationController(Voice, new TurnRouter(Connectivity, Usage), pipeline,
                new BargeInCoordinator(Output, Queue, speech, latency), Machine, switcher, keeper, Localizer, Clock, Log,
                Timings);
            _disposables.Add(speech);
            _disposables.Add(new ConversationStateRelay(Machine, Events, Character));
            _disposables.Add(Camera);

            Events.StateChanged += (_, to) => States.Add(to);
            Events.CaptionStarted += (_, text) => Captions.Add(text);
            Events.UserPartial += Partials.Add;
        }

        public ConversationState State => Machine.State;
        public UniTaskStatus RunStatus => _running.Status;

        public ConversationFixture Start()
        {
            _running = Controller.RunAsync(_app.Token).Preserve();
            Assert.That(_running.Status, Is.EqualTo(UniTaskStatus.Pending));
            return this;
        }

        public void Stop() => _app.Cancel();

        /// <summary>Press and release: one complete PTT gesture.</summary>
        public void Talk(string final = null)
        {
            if (final != null)
                Stt.Final = final;
            Voice.Press();
            Voice.Release();
        }

        public void Advance(double seconds) => Clock.Advance(TimeSpan.FromSeconds(seconds));

        public string StatePath() => string.Join(">", States);

        public void Dispose()
        {
            _app.Cancel();
            for (var i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
            TempDirectory.Delete(Directory);
        }
    }
}
