using System;
using Jolly.Contracts;
using Jolly.Core.Personas;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Personas
{
    public sealed class CharacterNamedLocalizerTests
    {
        FakeLocalizer _inner;

        sealed class DefaultVoice : ICharacterVoice
        {
            public VoiceSpec VoiceFor(string languageTag) => new();
        }

        [SetUp]
        public void SetUp()
        {
            _inner = new FakeLocalizer()
                .With(PersonaRepository.Table, "companion.prompt", "You are {name}, a small companion. {name} is kind.")
                .With(PersonaRepository.Table, "companion.greeting", "Hi, it's me, {name}!")
                .With(PersonaRepository.Table, "companion.name", "Companion")
                .With("pl", PersonaRepository.Table, "companion.prompt", "Jesteś {name}.")
                .With("pl", CharacterNamedLocalizer.NameTable, "character.jolly", "Dżoli");
        }

        [Test]
        public void Get_FillsEveryTokenWithTheDisplayName()
        {
            var text = new CharacterNamedLocalizer(_inner, "Jolly");

            Assert.That(text.Get(PersonaRepository.Table, "companion.prompt"),
                        Is.EqualTo("You are Jolly, a small companion. Jolly is kind."));
            Assert.That(text.Get(PersonaRepository.Table, "companion.name"), Is.EqualTo("Companion"));
        }

        [Test]
        public void Name_UsesTheLocalizedNameKeyWhenTheLocaleHasIt()
        {
            // Arrange
            var text = new CharacterNamedLocalizer(_inner, "Jolly", "character.jolly");

            // Act
            var english = text.Name;
            Sync.Run(text.SetLanguage("pl", default));

            // Assert
            Assert.That(english, Is.EqualTo("Jolly"), "no en entry: the display name");
            Assert.That(text.LanguageTag, Is.EqualTo("pl"));
            Assert.That(text.Get(PersonaRepository.Table, "companion.prompt"), Is.EqualTo("Jesteś Dżoli."));
        }

        [Test]
        public void Fill_TextWithoutTheToken_IsReturnedAsIs()
        {
            var text = new CharacterNamedLocalizer(_inner, "Jolly");
            const string plain = "Hello again!";

            Assert.That(text.Fill(plain), Is.SameAs(plain));
            Assert.That(text.Fill(null), Is.Null);
            Assert.That(CharacterNamedLocalizer.Fill("I'm {name}.", "Nubi"), Is.EqualTo("I'm Nubi."));
        }

        [Test]
        public void Constructor_RequiresADisplayName()
        {
            Assert.Throws<ArgumentException>(() => new CharacterNamedLocalizer(_inner, " "));
            Assert.Throws<ArgumentNullException>(() => new CharacterNamedLocalizer(null, "Jolly"));
        }

        [Test]
        public void PersonaRepository_ThroughIt_SaysTheCharactersNameAndKeepsThePromptStable()
        {
            // Arrange
            var definition = new PersonaDefinition
            {
                Id = "companion", DisplayNameKey = "companion.name", SystemPromptKey = "companion.prompt",
                GreetingKey = "companion.greeting", AllowedTools = Array.Empty<string>(), DefaultMood = Mood.Happy,
            };

            // Act
            using var personas = new PersonaRepository(new[] { definition }, "companion",
                                                       new CharacterNamedLocalizer(_inner, "Jolly"), new DefaultVoice());
            var first = personas.Current.SystemPrompt;
            Sync.Run(personas.ApplySwitch("companion", default));

            // Assert
            Assert.That(first, Is.EqualTo("You are Jolly, a small companion. Jolly is kind."));
            Assert.That(personas.Current.SystemPrompt, Is.EqualTo(first), "same prefix every time for one character");
            Assert.That(personas.GreetingFor("companion"), Is.EqualTo("Hi, it's me, Jolly!"));
            Assert.That(personas.Current.DisplayName, Is.EqualTo("Companion"));
        }

        [Test]
        public void Changed_IsTheInnerLocalizersEvent()
        {
            var text = new CharacterNamedLocalizer(_inner, "Jolly");
            var raised = 0;
            Action onChanged = () => raised++;
            text.Changed += onChanged;

            Sync.Run(_inner.SetLanguage("pl", default));
            text.Changed -= onChanged;
            Sync.Run(_inner.SetLanguage("en", default));

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(text.Voice, Is.SameAs(_inner.Voice));
            Assert.That(text.AvailableLanguageTags, Is.EquivalentTo(_inner.AvailableLanguageTags));
        }
    }
}
