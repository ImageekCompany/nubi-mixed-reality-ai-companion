using System.Linq;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Speech;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    public sealed class ConversationControllerTests
    {
        ConversationFixture _f;

        [SetUp]
        public void SetUp() => _f = new ConversationFixture().Start();

        [TearDown]
        public void TearDown() => _f.Dispose();

        [Test]
        public void VoiceTurn_ListensThinksSpeaksAndReturnsToIdle()
        {
            _f.Stt.Partials.Add("Hi newbie");
            _f.Agent.Say("Hello there. ").Say("How are you?");

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Partials, Is.EqualTo(new[] { "Hi Nubi" }));
            Assert.That(_f.Stt.LastOptions.SampleRate, Is.EqualTo(24000));
            Assert.That(_f.Mic.Last.Stopped, Is.False);

            _f.Voice.Release();

            Assert.That(_f.Mic.Last.Stopped, Is.True);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Thinking), "nothing has played yet");
            var turn = _f.Agent.Turns.Single();
            Assert.That(turn.Text, Is.EqualTo("Hi Nubi"));
            Assert.That(turn.LanguageTag, Is.EqualTo("en"));
            Assert.That(turn.PreCaptured, Is.Not.Null);
            Assert.That(_f.Queue.Enqueued.Select(e => e.sentence), Is.EqualTo(new[] { "Hello there.", "How are you?" }));
            Assert.That(_f.Queue.Enqueued.All(e => e.voice.VoiceId == "voice-companion"));

            _f.Queue.PlayNext();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
            Assert.That(_f.Captions, Is.EqualTo(new[] { "Hello there." }), "captions follow the first played sample");

            _f.Queue.Drain();

            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Thinking>Speaking>Idle"));
            Assert.That(_f.Character.States, Is.EqualTo(_f.States));
            Assert.That(_f.Captions, Is.EqualTo(new[] { "Hello there.", "How are you?" }));
            Assert.That(_f.Log.Contains(LogLevel.Info, "evt=turn.latency turn=1"), Is.True);
        }

        [Test]
        public void ToolTurn_ActsWhileTheToolRunsAndSpeaksOverIt()
        {
            _f.Agent.ToolStarted("walk_to").Say("On my way.").Hold().ToolCompleted("walk_to");
            var tools = 0;
            _f.Events.ToolStarted += (_, name) => tools++;

            _f.Talk("Walk to the sofa");
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting));
            Assert.That(tools, Is.EqualTo(1));

            _f.Queue.PlayNext();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking), "speaking wins over acting");
            _f.Queue.Drain();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Acting), "the tool is still running");

            _f.Agent.Release();

            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Thinking>Acting>Speaking>Acting>Thinking>Idle"));
        }

        [Test]
        public void EmptyFinal_ReturnsToIdleWithoutCallingTheAgent()
        {
            _f.Talk("  ");

            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Idle"));
            Assert.That(_f.Agent.Turns, Is.Empty);
        }

        [Test]
        public void SttFailure_ReturnsToIdleAndIsLogged()
        {
            _f.Stt.Failure = new ProviderException("asr", ProviderErrorKind.Network, "closed");

            _f.Talk();

            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Idle"));
            Assert.That(_f.Log.Contains(LogLevel.Warning, "evt=stt.failed kind=Network"), Is.True);
            Assert.That(_f.Mic.Last.Disposed, Is.True);
        }

        [Test]
        public void ShortStop_HaltsLocomotionBeforeTheModelAndStillRunsTheTurn()
        {
            _f.Talk("Stop, Nubi!");

            Assert.That(_f.Character.StopCount, Is.EqualTo(1));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("Stop, Nubi!"));
        }

        [Test]
        public void AgentFailure_SpeaksTheErrorLineAfterWhatWasAlreadySaid()
        {
            _f.Agent.Say("Let me see.").Fail();

            _f.Talk();
            Assert.That(_f.Offline.Categories, Is.Empty, "the error line waits for the current speech");
            _f.Queue.PlayNext();
            _f.Queue.Drain();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.Error }));
            Assert.That(_f.StatePath(), Is.EqualTo("Listening>Thinking>Speaking>Thinking>Speaking>Idle"));
        }

        [Test]
        public void CameraUnavailable_TurnHasNoPreCapturedFrame()
        {
            _f.Camera.Availability = CameraAvailability.NoPermission;

            _f.Talk();

            Assert.That(_f.Agent.Turns.Single().PreCaptured, Is.Null);
        }

        [Test]
        public void PreCaptureFailure_IsLoggedAndTheTurnContinues()
        {
            _f.Camera.FailNext = true;

            _f.Talk();

            Assert.That(_f.Agent.Turns.Single().PreCaptured, Is.Null);
            Assert.That(_f.Log.Contains(LogLevel.Warning, "evt=precapture.failed"), Is.True);
        }

        [Test]
        public void LocaleWithoutTts_ShowsCaptionsOnly()
        {
            _f.Dispose();
            _f = new ConversationFixture("pl").Start();
            _f.Agent.Say("Cześć. ").Say("Jak się masz?");

            _f.Talk("cześć");

            Assert.That(_f.Queue.Enqueued, Is.Empty);
            Assert.That(_f.Stt.LastOptions.LanguageBias, Is.EqualTo("Polish"));
            Assert.That(_f.Captions, Is.EqualTo(new[] { "Cześć." }), "one caption at a time");
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));

            _f.Advance(CaptionTiming.HoldFor("Cześć.").TotalSeconds);
            Assert.That(_f.Captions, Is.EqualTo(new[] { "Cześć.", "Jak się masz?" }));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking), "the last caption is still being read");

            _f.Advance(CaptionTiming.HoldFor("Jak się masz?").TotalSeconds);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void Acknowledge_PlaysAtReleaseNotAtPress()
        {
            _f.Timings.AcknowledgeOnRelease = true;
            _f.Agent.Hold();

            _f.Voice.Press();
            Assert.That(_f.Offline.Categories, Is.Empty);
            _f.Voice.Release();

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.Acknowledge }));
        }

        [Test]
        public void Acknowledge_IsSkippedWhenTheTurnEndedBeforeTheRelease()
        {
            _f.Timings.AcknowledgeOnRelease = true;
            _f.Stt.Failure = new ProviderException("stt", ProviderErrorKind.Server, "scripted");
            _f.Voice.Press();
            _f.Mic.Last.Stop();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle), "ASR failed while still pinching");

            _f.Voice.Release();

            Assert.That(_f.Offline.Categories, Is.Empty);
        }

        [Test]
        public void OfflinePressWhileSpeaking_GetsAFullSpeakingLimit()
        {
            _f.Agent.Say("Long answer. ").Hold();
            _f.Talk();
            _f.Queue.PlayNext();
            _f.Advance(59);
            _f.Connectivity.State = Connectivity.Offline;
            _f.Offline.Hold = true;

            _f.Voice.Press();
            _f.Advance(2);

            Assert.That(_f.Log.Contains(LogLevel.Warning, "evt=watchdog"), Is.False);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
            _f.Offline.Release();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void LocaleChangeMidTurn_EndsTheTurn()
        {
            _f.Agent.Say("Hello there. ").Hold();
            _f.Talk();
            _f.Queue.PlayNext();

            Sync.Run(_f.Localizer.SetLanguage("pl", default));

            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
            Assert.That(_f.Agent.InterruptedSpoken, Is.EqualTo("Hello there. "));
        }

        [Test]
        public void TurnEnd_SavesConversationMemory()
        {
            _f.Agent.Say("Hi.");

            _f.Talk("hello");
            _f.Queue.Drain();

            Assert.That(_f.Memory.Recent, Is.Empty, "the scripted agent commits nothing");
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(_f.Directory, "conversation.companion.json")), Is.True);
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(_f.Directory, "scene.json")), Is.True);
        }

        [Test]
        public void AppCancel_CompletesRunAndDetachesFromPtt()
        {
            _f.Stop();

            Assert.That(_f.RunStatus, Is.EqualTo(UniTaskStatus.Succeeded));
            _f.Voice.Press();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
            Assert.That(_f.Mic.Sessions, Is.Empty);
        }
    }
}
