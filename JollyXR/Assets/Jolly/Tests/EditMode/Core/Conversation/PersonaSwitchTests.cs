using System.Linq;
using Jolly.Contracts;
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
                FakePersonaProvider.Profile("psychologist", persistMemory: false, crisisHandoff: true,
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
