using System;
using System.Linq;
using Jolly.Contracts;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Conversation
{
    /// <summary>Architecture §3: a switch is recorded during a turn and applied only once the conversation is Idle.</summary>
    public sealed class PersonaSwitchTests
    {
        ConversationFixture _f;

        [SetUp]
        public void SetUp()
        {
            _f = new ConversationFixture("en", FakePersonaProvider.Profile("companion"), FakePersonaProvider.Profile("guru"),
                FakePersonaProvider.Profile("psychologist", persistMemory: false,
                    safetyNotice: "I am not a therapist."));
            _f.Personas.Greetings["guru"] = "Greetings, seeker.";
            _f.Personas.Greetings["psychologist"] = "Hello, I am here to listen.";
            _f.Start();
        }

        [TearDown]
        public void TearDown() => _f.Dispose();

        [Test]
        public void RequestDuringATurn_IsAppliedWhenIdle_AndGreetsInTheNewVoice()
        {
            _f.Agent.Say("Switching now.").Hold();
            _f.Talk("Switch to Guru");
            _f.Queue.PlayNext();

            _f.Personas.RequestSwitch("guru");
            Assert.That(_f.Personas.ApplyCount, Is.Zero);
            _f.Agent.Release();
            Assert.That(_f.Personas.ApplyCount, Is.Zero, "speech is still draining");

            _f.Queue.Drain();

            Assert.That(_f.Personas.Current.Id, Is.EqualTo("guru"));
            var greeting = _f.Queue.Enqueued.Last();
            Assert.That(greeting.sentence, Is.EqualTo("Greetings, seeker."));
            Assert.That(greeting.voice.VoiceId, Is.EqualTo("voice-guru"));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));

            _f.Queue.Drain();
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void RequestWhileIdle_AppliesAtOnce()
        {
            _f.Personas.RequestSwitch("guru");

            Assert.That(_f.Personas.ApplyCount, Is.EqualTo(1));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Speaking));
        }

        [Test]
        public void RequestWhileListening_WaitsForTheTurn()
        {
            _f.Voice.Press();

            _f.Personas.RequestSwitch("guru");

            Assert.That(_f.Personas.ApplyCount, Is.Zero);
        }

        [Test]
        public void SwitchSavesTheOldMemoryAndCarriesTheSummaryBetweenPersistingPersonas()
        {
            Jolly.Tests.EditMode.Core.Sync.Run(_f.Memory.Load("companion", "Likes tea.", default));

            _f.Personas.RequestSwitch("guru");

            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(_f.Directory, "conversation.companion.json")), Is.True);
            Assert.That(_f.Memory.Summary, Is.EqualTo("Likes tea."));
        }

        [Test]
        public void SwitchToANonPersistingPersona_CarriesNothing_AndSpeaksTheSafetyNotice()
        {
            Jolly.Tests.EditMode.Core.Sync.Run(_f.Memory.Load("companion", "Likes tea.", default));

            _f.Personas.RequestSwitch("psychologist");

            Assert.That(_f.Memory.Summary, Is.Empty);
            Assert.That(_f.Queue.Enqueued.Select(e => e.sentence),
                Is.EqualTo(new[] { "Hello, I am here to listen.", "I am not a therapist." }));
        }

        [Test]
        public void SafetyNotice_IsCaptionedEvenWhenTtsNeverPlaysIt()
        {
            _f.Personas.RequestSwitch("psychologist");
            _f.Queue.PlayNext();

            _f.Queue.DrainUnplayed();

            Assert.That(_f.Captions, Is.EqualTo(new[] { "Hello, I am here to listen.", "I am not a therapist." }));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void SwitchWithoutNetwork_GivesTheCannedSafetyNotice()
        {
            _f.Connectivity.State = Connectivity.Offline;

            _f.Personas.RequestSwitch("psychologist");

            Assert.That(_f.Offline.Categories, Is.EqualTo(new[] { LineCategory.SafetyNotice }));
            Assert.That(_f.Queue.Enqueued, Is.Empty);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void StartingInAPersonaWithASafetyNotice_GivesItWithoutAGreeting()
        {
            _f.Dispose();
            _f = new ConversationFixture("en", FakePersonaProvider.Profile("psychologist", persistMemory: false,
                safetyNotice: "I am not a therapist."));
            _f.Personas.Greetings["psychologist"] = "Hello, I am here to listen.";

            _f.Start();

            Assert.That(_f.Queue.Enqueued.Select(e => e.sentence), Is.EqualTo(new[] { "I am not a therapist." }));
            _f.Queue.Drain();
            Assert.That(_f.Captions, Is.EqualTo(new[] { "I am not a therapist." }));
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void StartingInAPersonaWithoutASafetyNotice_SaysNothing()
        {
            Assert.That(_f.Queue.Enqueued, Is.Empty);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void SaveFailure_StillSwitches_AndLaterTurnsKeepSaving()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_f.Directory, "conversation.companion.json"));

            _f.Personas.RequestSwitch("guru");
            _f.Queue.Drain();
            _f.Agent.Say("Hi.");
            _f.Talk("hello");
            _f.Queue.Drain();

            Assert.That(_f.Log.Contains(LogLevel.Error, "evt=memory.save_failed"), Is.True);
            Assert.That(_f.Personas.Current.Id, Is.EqualTo("guru"));
            Assert.That(_f.Personas.PendingSwitchId, Is.Null);
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(_f.Directory, "conversation.guru.json")), Is.True);
        }

        [Test]
        public void RefusedSwitch_KeepsThePersonaAndSaysNothing()
        {
            _f.Personas.RefuseSwitch = true;

            _f.Personas.RequestSwitch("guru");

            Assert.That(_f.Personas.Current.Id, Is.EqualTo("companion"));
            Assert.That(_f.Log.Contains(LogLevel.Warning, "evt=persona.switch_failed id=guru"), Is.True);
            Assert.That(_f.Queue.Enqueued, Is.Empty);
            Assert.That(_f.State, Is.EqualTo(ConversationState.Idle));
        }

        [Test]
        public void CrashingSwitch_IsLogged_AndDoesNotLockOutPushToTalk()
        {
            _f.Personas.SwitchFailure = new InvalidOperationException("bug");

            _f.Personas.RequestSwitch("guru");
            _f.Agent.Say("Still here.");
            _f.Talk("hello");
            _f.Queue.Drain();

            Assert.That(_f.Log.Contains(LogLevel.Error, "evt=persona.switch_crashed"), Is.True);
            Assert.That(_f.Personas.Current.Id, Is.EqualTo("companion"));
            Assert.That(_f.Agent.Turns.Single().Text, Is.EqualTo("hello"), "a press during a switch is ignored, so a stuck switch would drop it");
            Assert.That(_f.Captions, Is.EqualTo(new[] { "Still here." }));
        }

        [Test]
        public void PressDuringTheGreeting_BargesIn()
        {
            _f.Personas.RequestSwitch("guru");
            _f.Queue.PlayNext();

            _f.Voice.Press();

            Assert.That(_f.State, Is.EqualTo(ConversationState.Listening));
            Assert.That(_f.Output.Flushes, Is.EqualTo(1));
        }
    }
}
