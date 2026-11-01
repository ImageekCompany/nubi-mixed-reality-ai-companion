using Jolly.Contracts;
using Jolly.Core.Personas;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Personas
{
    public sealed class VoiceDeliveryTests
    {
        static VoiceSpec Rubie() => new()
        {
            VoiceId = "Rubie", Style = "default", Speed = 100, Pitch = 110, SfxCharacter = "robot", SfxEnvironment = "room",
        };

        [Test]
        public void Defaults_SpeakTheCharacterVoiceUnchanged()
        {
            var voice = new VoiceDelivery().ApplyTo(Rubie());

            Assert.That(voice.VoiceId, Is.EqualTo("Rubie"));
            Assert.That(voice.Style, Is.EqualTo("default"));
            Assert.That(voice.Speed, Is.EqualTo(100));
            Assert.That(voice.Pitch, Is.EqualTo(110));
            Assert.That(voice.SfxCharacter, Is.EqualTo("robot"));
            Assert.That(voice.SfxEnvironment, Is.EqualTo("room"));
        }

        [Test]
        public void Percentages_ScaleTheCharactersSpeedAndPitch()
        {
            var guru = new VoiceDelivery { SpeedPercent = 90, PitchPercent = 92 }.ApplyTo(Rubie());

            Assert.That(guru.Speed, Is.EqualTo(90));
            Assert.That(guru.Pitch, Is.EqualTo(101), "110 × 0.92 = 101.2");
        }

        [Test]
        public void Style_ReplacesTheCharactersOnlyWhenSet()
        {
            Assert.That(new VoiceDelivery { Style = "soft" }.ApplyTo(Rubie()).Style, Is.EqualTo("soft"));
            Assert.That(new VoiceDelivery { Style = "" }.ApplyTo(Rubie()).Style, Is.EqualTo("default"));
        }

        [Test]
        public void Results_AreClampedToWitRanges()
        {
            var fast = new VoiceDelivery { SpeedPercent = 150, PitchPercent = 150 }
                .ApplyTo(new VoiceSpec { Speed = 180, Pitch = 180 });
            var slow = new VoiceDelivery { SpeedPercent = 50, PitchPercent = 50 }
                .ApplyTo(new VoiceSpec { Speed = 60, Pitch = 30 });

            Assert.That(fast.Speed, Is.EqualTo(VoiceSpec.MaxSpeed));
            Assert.That(fast.Pitch, Is.EqualTo(VoiceSpec.MaxPitch));
            Assert.That(slow.Speed, Is.EqualTo(VoiceSpec.MinSpeed));
            Assert.That(slow.Pitch, Is.EqualTo(VoiceSpec.MinPitch));
        }

        [Test]
        public void ApplyTo_NeverModifiesTheCharactersSpec()
        {
            var character = Rubie();

            var voice = new VoiceDelivery { Style = "soft", SpeedPercent = 80, PitchPercent = 80 }.ApplyTo(character);

            Assert.That(voice, Is.Not.SameAs(character));
            Assert.That(character.Style, Is.EqualTo("default"));
            Assert.That(character.Speed, Is.EqualTo(100));
            Assert.That(character.Pitch, Is.EqualTo(110));
        }
    }
}
