using System;
using System.Text;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Asr;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class MuseTranscribeRequestTests
    {
        [Test]
        public void Wav_HasACanonical44ByteMonoS16Header()
        {
            var wav = MuseTranscribeRequest.EncodeWav(new short[] { 1, -2, 3 }, 24000);

            Assert.That(wav.Length, Is.EqualTo(44 + 6));
            Assert.That(Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(BitConverter.ToInt32(wav, 4), Is.EqualTo(36 + 6));
            Assert.That(Encoding.ASCII.GetString(wav, 8, 8), Is.EqualTo("WAVEfmt "));
            Assert.That(BitConverter.ToInt16(wav, 20), Is.EqualTo(1), "PCM");
            Assert.That(BitConverter.ToInt16(wav, 22), Is.EqualTo(1), "mono");
            Assert.That(BitConverter.ToInt32(wav, 24), Is.EqualTo(24000));
            Assert.That(BitConverter.ToInt32(wav, 28), Is.EqualTo(48000), "byte rate");
            Assert.That(BitConverter.ToInt16(wav, 34), Is.EqualTo(16));
            Assert.That(Encoding.ASCII.GetString(wav, 36, 4), Is.EqualTo("data"));
            Assert.That(BitConverter.ToInt32(wav, 40), Is.EqualTo(6));
            Assert.That(BitConverter.ToInt16(wav, 46), Is.EqualTo(-2));
        }

        [Test]
        public void Body_IsMultipartWithTheJsonRequestPartThenTheWavPart()
        {
            var wav = new byte[] { 0x52, 0x49, 0x46, 0x46 };

            var body = MuseTranscribeRequest.BuildBody(wav, "muse-voice-transcribe-1.0", new SttOptions(), "BOUNDARY");
            var text = Encoding.UTF8.GetString(body);

            Assert.That(text, Is.EqualTo(
                "--BOUNDARY\r\n" +
                "Content-Disposition: form-data; name=\"request\"\r\n" +
                "Content-Type: application/json\r\n\r\n" +
                "{\"model\":\"muse-voice-transcribe-1.0\",\"audioEncoding\":\"WAV\",\"languageBias\":[\"English\"]}\r\n" +
                "--BOUNDARY\r\n" +
                "Content-Disposition: form-data; name=\"audio\"; filename=\"audio.wav\"\r\n" +
                "Content-Type: audio/wav\r\n\r\n" +
                "RIFF\r\n" +
                "--BOUNDARY--\r\n"));
            Assert.That(MuseTranscribeRequest.ContentType("BOUNDARY"), Is.EqualTo("multipart/form-data; boundary=BOUNDARY"));
        }

        [Test]
        public void Response_RecordedJson_GivesTranscriptAndDuration()
        {
            var e = MuseTranscribeRequest.ParseResponse(AsrFixtures.Text("transcribe_response.json"));

            Assert.That(e.Kind, Is.EqualTo(TranscriptKind.Final));
            Assert.That(e.Text, Is.EqualTo("Hey Jolly, walk to the kitchen and tell me what this pen is."));
            Assert.That(e.AudioMs, Is.EqualTo(3280));
        }

        [Test]
        public void Response_WithoutTranscript_ThrowsProtocol()
        {
            var e = Assert.Throws<ProviderException>(() => MuseTranscribeRequest.ParseResponse("{\"sessionId\":\"x\"}"));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        }
    }
}
