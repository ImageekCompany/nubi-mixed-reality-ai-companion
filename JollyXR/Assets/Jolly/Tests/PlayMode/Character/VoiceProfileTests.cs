using Jolly.Character;
using Jolly.Contracts;
using Jolly.Tests.PlayMode.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.Character
{
    public sealed class VoiceProfileTests
    {
        VoiceProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<VoiceProfile>();
            _profile.defaultVoice = new VoiceProfile.Voice { voiceId = "Rubie", speed = 100, pitch = 110 };
            _profile.locales = new[]
            {
                new VoiceProfile.LocaleVoice
                {
                    languageTag = "pl",
                    voice = new VoiceProfile.Voice { voiceId = "Ola", style = "soft", speed = 95, pitch = 105 },
                },
                new VoiceProfile.LocaleVoice { languageTag = "en-GB", voice = new VoiceProfile.Voice { voiceId = "Whimsical" } },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_profile);

        [Test]
        public void VoiceFor_LanguageWithoutEntry_IsTheDefaultVoice()
        {
            var voice = _profile.VoiceFor("en");

            Assert.That(voice.VoiceId, Is.EqualTo("Rubie"));
            Assert.That(voice.Speed, Is.EqualTo(100));
            Assert.That(voice.Pitch, Is.EqualTo(110));
        }

        [Test]
        public void VoiceFor_EmptyStyleAndSfx_AreNullSoTheyAreNotSent()
        {
            var voice = _profile.VoiceFor("en");

            Assert.That(voice.Style, Is.Null);
            Assert.That(voice.SfxCharacter, Is.Null);
            Assert.That(voice.SfxEnvironment, Is.Null);
        }

        [Test]
        public void VoiceFor_LocaleEntry_ReplacesTheWholeVoice()
        {
            var voice = _profile.VoiceFor("pl");

            Assert.That(voice.VoiceId, Is.EqualTo("Ola"));
            Assert.That(voice.Style, Is.EqualTo("soft"));
            Assert.That(voice.Speed, Is.EqualTo(95));
            Assert.That(voice.Pitch, Is.EqualTo(105));
        }

        [TestCase("PL", "Ola")]
        [TestCase("pl-PL", "Ola")]
        [TestCase("pl_PL", "Ola")]
        [TestCase("en-GB", "Whimsical")]
        [TestCase("en-US", "Rubie")]
        [TestCase("plx", "Rubie")]
        [TestCase(null, "Rubie")]
        public void VoiceFor_MatchesExactTagThenPrimaryLanguage(string tag, string expected)
        {
            Assert.That(_profile.VoiceFor(tag).VoiceId, Is.EqualTo(expected));
        }

        [Test]
        public void VoiceFor_Sfx_IsPassedThrough()
        {
            _profile.defaultVoice.sfxCharacter = "chipmunk";
            _profile.defaultVoice.sfxEnvironment = "room";

            var voice = _profile.VoiceFor("en");

            Assert.That(voice.SfxCharacter, Is.EqualTo("chipmunk"));
            Assert.That(voice.SfxEnvironment, Is.EqualTo("room"));
        }

        [Test]
        public void VoiceFor_ReturnsAFreshSpecEachCall()
        {
            var first = _profile.VoiceFor("en");
            first.Pitch = 25;

            Assert.That(_profile.VoiceFor("en").Pitch, Is.EqualTo(110));
        }

        [Test]
        public void ShippedNubi_SpeaksWithTheAuditionWinner()
        {
            var nubi = PresentationAssets.Load<CharacterDefinition>("Assets/Jolly/Content/Character/Nubi/NubiCharacter.asset");

            var voice = nubi.voice.VoiceFor("en");

            Assert.That(voice.VoiceId, Is.EqualTo("Rubie"), "ADR-0031");
            Assert.That(voice.Style, Is.Null);
            Assert.That(voice.Speed, Is.EqualTo(100));
            Assert.That(voice.Pitch, Is.EqualTo(110));
            Assert.That(voice.SfxCharacter, Is.Null);
            Assert.That(voice.SfxEnvironment, Is.Null);
        }
    }
}
