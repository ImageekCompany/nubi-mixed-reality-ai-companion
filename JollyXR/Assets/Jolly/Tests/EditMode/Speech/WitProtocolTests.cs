using System;
using Jolly.Contracts;
using Jolly.Providers.Wit;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class WitProtocolTests
    {
        [Test]
        public void Body_ContainsTextVoiceSpeedAndPitch()
        {
            var body = WitProtocol.SynthesizeBody("Hi there.", new VoiceSpec { VoiceId = "Charlie", Speed = 110, Pitch = 90 });

            Assert.That(body, Is.EqualTo("{\"q\":\"Hi there.\",\"voice\":\"Charlie\",\"speed\":110,\"pitch\":90}"));
        }

        [Test]
        public void Body_NubiVoice_IsTheAuditionedRequest()
        {
            var body = WitProtocol.SynthesizeBody("Hey!", new VoiceSpec { VoiceId = "Rubie", Speed = 100, Pitch = 110 });

            Assert.That(body, Is.EqualTo("{\"q\":\"Hey!\",\"voice\":\"Rubie\",\"speed\":100,\"pitch\":110}"));
        }

        [Test]
        public void Body_WithStyle_IncludesIt()
        {
            var body = WitProtocol.SynthesizeBody("Hi", new VoiceSpec { VoiceId = "Rebecca", Style = "soft" });

            Assert.That(body, Is.EqualTo("{\"q\":\"Hi\",\"voice\":\"Rebecca\",\"style\":\"soft\",\"speed\":100,\"pitch\":100}"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Body_WithoutStyle_OmitsTheKey(string style)
        {
            var body = WitProtocol.SynthesizeBody("Hi", new VoiceSpec { Style = style });

            Assert.That(body, Does.Not.Contain("\"style\""));
        }

        [Test]
        public void Body_WithoutEffect_SendsPlainTextAndNoGain()
        {
            var body = WitProtocol.SynthesizeBody("Hi <3", new VoiceSpec { VoiceId = "Rubie", SfxCharacter = "", SfxEnvironment = null });

            Assert.That(body, Is.EqualTo("{\"q\":\"Hi <3\",\"voice\":\"Rubie\",\"speed\":100,\"pitch\":100}"),
                "Wit ignores gain, and text outside SSML must not be XML-escaped");
        }

        [Test]
        public void Body_SfxCharacter_WrapsTheTextInSsml()
        {
            var body = WitProtocol.SynthesizeBody("Hi.", new VoiceSpec { VoiceId = "Charlie", SfxCharacter = "chipmunk" });

            Assert.That(body, Does.StartWith(
                "{\"q\":\"<speak><sfx character=\\\"chipmunk\\\">Hi.</sfx></speak>\",\"voice\":\"Charlie\","));
        }

        [Test]
        public void Body_SfxEnvironment_WrapsTheTextInSsml()
        {
            var body = WitProtocol.SynthesizeBody("Hi.", new VoiceSpec { SfxEnvironment = "room" });

            Assert.That(body, Does.StartWith("{\"q\":\"<speak><sfx environment=\\\"room\\\">Hi.</sfx></speak>\","));
        }

        [Test]
        public void Body_BothSfx_PutCharacterBeforeEnvironment()
        {
            var body = WitProtocol.SynthesizeBody("Hi.", new VoiceSpec { SfxCharacter = "robot", SfxEnvironment = "radio" });

            Assert.That(body, Does.Contain("<sfx character=\\\"robot\\\" environment=\\\"radio\\\">Hi.</sfx>"));
        }

        [Test]
        public void Body_Ssml_EscapesMarkupButKeepsApostrophes()
        {
            var body = WitProtocol.SynthesizeBody("Tom & Jerry <3 \"you're\"", new VoiceSpec { SfxCharacter = "a\"b" });

            Assert.That(body, Does.Contain(
                "<sfx character=\\\"a&quot;b\\\">Tom &amp; Jerry &lt;3 &quot;you're&quot;</sfx>"));
        }

        [Test]
        public void Body_EscapesQuotesBackslashesAndControlCharacters()
        {
            var body = WitProtocol.SynthesizeBody("say \"hi\"\\\n\u0001", new VoiceSpec());

            Assert.That(body, Does.StartWith("{\"q\":\"say \\\"hi\\\"\\\\\\n\\u0001\","));
        }

        [TestCase(401, ProviderErrorKind.Unauthorized)]
        [TestCase(403, ProviderErrorKind.Unauthorized)]
        [TestCase(429, ProviderErrorKind.RateLimited)]
        [TestCase(400, ProviderErrorKind.BadRequest)]
        [TestCase(413, ProviderErrorKind.BadRequest)]
        [TestCase(500, ProviderErrorKind.Server)]
        [TestCase(503, ProviderErrorKind.Server)]
        public void Error_HttpStatus_MapsToKind(int status, ProviderErrorKind expected)
        {
            var error = WitProtocol.Error(status, false, null, "HTTP/1.1 " + status);

            Assert.That(error.Kind, Is.EqualTo(expected));
            Assert.That(error.HttpStatus, Is.EqualTo(status));
            Assert.That(error.Provider, Is.EqualTo(WitProtocol.Provider));
        }

        [Test]
        public void Error_ConnectionFailure_IsNetwork()
        {
            var error = WitProtocol.Error(0, true, null, "Cannot resolve destination host");

            Assert.That(error.Kind, Is.EqualTo(ProviderErrorKind.Network));
            Assert.That(error.IsTransient, Is.True);
        }

        [Test]
        public void Error_RetryAfterSeconds_IsParsed()
        {
            var error = WitProtocol.Error(429, false, "7", "Too Many Requests");

            Assert.That(error.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(7)));
        }

        [Test]
        public void TextToSpeech_SupportsEnglishOnly()
        {
            var tts = new WitTextToSpeech(new InMemorySecrets(), new FakeClock());

            Assert.That(tts.Supports("en"), Is.True);
            Assert.That(tts.Supports("en-GB"), Is.True);
            Assert.That(tts.Supports("pl"), Is.False);
            Assert.That(tts.SampleRate, Is.EqualTo(24000));
            Assert.That(tts.MaxCharacters, Is.EqualTo(240));
        }

        [TestCase("EN", true)]
        [TestCase("en_US", true)]
        [TestCase("EN-us", true)]
        [TestCase("eng", false)]
        [TestCase("es-ES", false)]
        [TestCase(null, false)]
        public void TextToSpeech_Supports_IgnoresCaseAndSeparator(string tag, bool expected)
        {
            var tts = new WitTextToSpeech(new InMemorySecrets(), new FakeClock());

            Assert.That(tts.Supports(tag), Is.EqualTo(expected));
        }

        [Test]
        public void TextToSpeech_WithoutToken_FailsAsUnauthorizedWithoutNetwork()
        {
            var tts = new WitTextToSpeech(new InMemorySecrets(), new FakeClock());

            var probe = StreamProbe<PcmChunk>.Start(tts.Synthesize("Hello.", new VoiceSpec(), default));

            Assert.That(probe.Error, Is.InstanceOf<ProviderException>());
            Assert.That(((ProviderException)probe.Error).Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
        }

        [Test]
        public void TextToSpeech_TextOverLimit_Throws()
        {
            var tts = new WitTextToSpeech(new InMemorySecrets().With(SecretKeys.WitClientToken, "x"), new FakeClock());

            Assert.Throws<ArgumentException>(() => tts.Synthesize(new string('a', 241), new VoiceSpec(), default));
        }
    }
}
