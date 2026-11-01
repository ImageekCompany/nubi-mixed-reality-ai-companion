using System.Collections.Generic;
using System.Threading;
using Jolly.Contracts;
using Jolly.Core.Personas;
using Jolly.Testing;
using Jolly.Testing.Conversation;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Personas
{
    public sealed class PersonaRepositoryTests
    {
        static readonly CancellationToken None = CancellationToken.None;
        FakeLocalizer _localizer;
        PerLanguageVoice _characterVoice;

        sealed class PerLanguageVoice : ICharacterVoice
        {
            public readonly Dictionary<string, VoiceSpec> Voices = new();
            public VoiceSpec VoiceFor(string languageTag) => Voices[languageTag].Copy();
        }

        [SetUp]
        public void SetUp()
        {
            _localizer = new FakeLocalizer()
                .With(PersonaRepository.Table, "companion.name", "Companion")
                .With(PersonaRepository.Table, "companion.prompt", "You are a warm companion.")
                .With(PersonaRepository.Table, "guru.name", "Guru")
                .With(PersonaRepository.Table, "guru.prompt", "You are a calm guru.")
                .With(PersonaRepository.Table, "guru.greeting", "Peace. I am here.")
                .With(PersonaRepository.Table, "psychologist.notice", "I am not a therapist.")
                .With("pl", PersonaRepository.Table, "companion.prompt", "Jesteś ciepłym towarzyszem.");
            _characterVoice = new PerLanguageVoice();
            _characterVoice.Voices["en"] = new VoiceSpec { VoiceId = "Rubie", Speed = 100, Pitch = 110 };
            _characterVoice.Voices["pl"] = new VoiceSpec { VoiceId = "Polish", Speed = 100, Pitch = 100 };
        }

        static PersonaDefinition Definition(string id, bool persist = true, string notice = null) => new()
        {
            Id = id, DisplayNameKey = id + ".name", SystemPromptKey = id + ".prompt", SafetyNoticeKey = notice,
            GreetingKey = id + ".greeting", AllowedTools = new[] { "stop" }, PersistMemory = persist,
            DefaultMood = Mood.Happy,
            Delivery = id == "guru" ? new VoiceDelivery { Style = "soft", SpeedPercent = 90, PitchPercent = 92 } : null,
        };

        PersonaRepository Create() => new(new[]
        {
            Definition("companion"), Definition("guru"),
            Definition("psychologist", persist: false, notice: "psychologist.notice"),
        }, "companion", _localizer, _characterVoice);

        [Test]
        public void Current_IsTheDefaultResolvedForTheLocale()
        {
            using var personas = Create();

            var p = personas.Current;

            Assert.That(p.Id, Is.EqualTo("companion"));
            Assert.That(p.DisplayName, Is.EqualTo("Companion"));
            Assert.That(p.SystemPrompt, Is.EqualTo("You are a warm companion."));
            Assert.That(p.SafetyNotice, Is.Null);
            Assert.That(p.Voice.VoiceId, Is.EqualTo("Rubie"), "the character's voice");
            Assert.That(p.Voice.Pitch, Is.EqualTo(110));
            Assert.That(p.AllowedTools, Is.EqualTo(new[] { "stop" }));
            Assert.That(p.PersistMemory, Is.True);
            Assert.That(p.DefaultMood, Is.EqualTo(Mood.Happy));
            Assert.That(personas.AvailableIds, Is.EqualTo(new[] { "companion", "guru", "psychologist" }));
        }

        [Test]
        public void RequestSwitch_OnlyRecordsAndNeverChangesCurrent()
        {
            using var personas = Create();
            var changed = 0;
            var requested = 0;
            personas.Changed += _ => changed++;
            personas.SwitchRequested += () => requested++;

            Assert.That(personas.RequestSwitch("guru"), Is.True);
            Assert.That(personas.RequestSwitch("pirate"), Is.False);

            Assert.That(personas.PendingSwitchId, Is.EqualTo("guru"));
            Assert.That(personas.Current.Id, Is.EqualTo("companion"));
            Assert.That(changed, Is.Zero);
            Assert.That(requested, Is.EqualTo(1), "only an accepted request is announced");
        }

        [Test]
        public void ApplySwitch_ResolvesClearsPendingAndRaisesChanged()
        {
            using var personas = Create();
            PersonaProfile raised = null;
            personas.Changed += p => raised = p;
            personas.RequestSwitch("psychologist");

            var ok = Sync.Run(personas.ApplySwitch("psychologist", None));

            Assert.That(ok, Is.True);
            Assert.That(personas.PendingSwitchId, Is.Null);
            Assert.That(raised, Is.SameAs(personas.Current));
            Assert.That(raised.SafetyNotice, Is.EqualTo("I am not a therapist."));
            Assert.That(raised.PersistMemory, Is.False);
        }

        [Test]
        public void ApplySwitch_SpeaksTheCharactersVoiceWithThePersonasDelivery()
        {
            using var personas = Create();

            Sync.Run(personas.ApplySwitch("guru", None));

            var voice = personas.Current.Voice;
            Assert.That(voice.VoiceId, Is.EqualTo("Rubie"));
            Assert.That(voice.Style, Is.EqualTo("soft"));
            Assert.That(voice.Speed, Is.EqualTo(90));
            Assert.That(voice.Pitch, Is.EqualTo(101));
        }

        [Test]
        public void Constructor_WithoutCharacterVoice_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new PersonaRepository(new[] { Definition("companion") }, "companion", _localizer, null));
        }

        [Test]
        public void ApplySwitch_UnknownId_ReturnsFalse()
        {
            using var personas = Create();

            Assert.That(Sync.Run(personas.ApplySwitch("pirate", None)), Is.False);
            Assert.That(personas.Current.Id, Is.EqualTo("companion"));
        }

        [Test]
        public void LocaleChange_ReResolvesCurrentWithTheCharactersVoiceForThatLanguage()
        {
            using var personas = Create();
            var changed = 0;
            personas.Changed += _ => changed++;

            Sync.Run(_localizer.SetLanguage("pl", None));

            Assert.That(personas.Current.SystemPrompt, Is.EqualTo("Jesteś ciepłym towarzyszem."));
            Assert.That(personas.Current.Voice.VoiceId, Is.EqualTo("Polish"));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_UnsubscribesFromLocaleChanges()
        {
            var personas = Create();
            var changed = 0;
            personas.Changed += _ => changed++;

            personas.Dispose();
            Sync.Run(_localizer.SetLanguage("pl", None));

            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void Greeting_ResolvesOrIsNullWhenMissing()
        {
            using var personas = Create();

            Assert.That(personas.GreetingFor("guru"), Is.EqualTo("Peace. I am here."));
            Assert.That(personas.GreetingFor("companion"), Is.Null);
            Assert.That(personas.GreetingFor("pirate"), Is.Null);
        }
    }
}
