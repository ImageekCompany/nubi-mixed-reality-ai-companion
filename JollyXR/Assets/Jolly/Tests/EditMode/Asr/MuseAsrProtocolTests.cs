using Jolly.Contracts;
using Jolly.Providers.MetaApi.Asr;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class MuseAsrProtocolTests
    {
        [Test]
        public void Handshake_HasAuthInTheBodyAndThePushToTalkConfig()
        {
            var json = MuseAsrHandshake.Build("KEY", "muse-voice-transcribe-1.0", new SttOptions());

            Assert.That(json, Is.EqualTo(
                "{\"authorization\":{\"accessToken\":\"Bearer KEY\"},\"audioEncoding\":\"PCM_24KHZ\"," +
                "\"model\":\"muse-voice-transcribe-1.0\",\"mode\":\"PUSH_TO_TALK\",\"partialMode\":\"CUMULATIVE\"," +
                "\"emitAudioProgress\":false,\"languageBias\":[\"English\"]}"));
        }

        [Test]
        public void Handshake_At16KHz_DeclaresPcm16KHz()
        {
            var json = MuseAsrHandshake.Build("KEY", "m", new SttOptions { SampleRate = 16000, LanguageBias = "Polish" });

            Assert.That(json, Does.Contain("\"audioEncoding\":\"PCM_16KHZ\""));
            Assert.That(json, Does.Contain("\"languageBias\":[\"Polish\"]"));
        }

        [Test]
        public void Handshake_WithoutLanguageBias_OmitsTheField()
        {
            var json = MuseAsrHandshake.Build("KEY", "m", new SttOptions { LanguageBias = null });

            Assert.That(json, Does.Not.Contain("languageBias"));
        }

        [Test]
        public void Handshake_EscapesTheKeyAsJson()
        {
            var json = MuseAsrHandshake.Build("a\"b", "m", new SttOptions());

            Assert.That(json, Does.Contain("\"Bearer a\\\"b\""));
        }

        [Test]
        public void Handshake_RejectsRatesTheServerDoesNotAccept()
        {
            var e = Assert.Throws<ProviderException>(() =>
                MuseAsrHandshake.Build("KEY", "m", new SttOptions { SampleRate = 44100 }));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.BadRequest));
        }

        [Test]
        public void EndStream_IsTheDocumentedTextFrame()
        {
            Assert.That(MuseAsrHandshake.EndStream, Is.EqualTo("{\"type\":\"endStream\"}"));
        }
    }
}
