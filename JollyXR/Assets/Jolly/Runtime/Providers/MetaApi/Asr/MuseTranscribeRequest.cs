using System;
using System.IO;
using System.Text;
using Jolly.Contracts;
using Newtonsoft.Json;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Wire format of <c>POST /v1/asr/transcribe</c> (meta-model-api.md §5.1, verified live): multipart with a JSON
    /// <c>request</c> part and a WAV <c>audio</c> part (mono s16le at 16 or 24 kHz), answered with
    /// <c>{"transcript":…,"audioDurationMs":…}</c>.
    /// </summary>
    public static class MuseTranscribeRequest
    {
        public static string ContentType(string boundary) => "multipart/form-data; boundary=" + boundary;

        public static byte[] EncodeWav(ReadOnlySpan<short> samples, int sampleRate)
        {
            var dataBytes = samples.Length * 2;
            var wav = new byte[44 + dataBytes];
            using (var w = new BinaryWriter(new MemoryStream(wav)))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(sampleRate);
                w.Write(sampleRate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var s in samples)
                    w.Write(s);
            }
            return wav;
        }

        public static byte[] BuildBody(byte[] wav, string model, SttOptions options, string boundary)
        {
            using var body = new MemoryStream(wav.Length + 512);
            Append(body, $"--{boundary}\r\nContent-Disposition: form-data; name=\"request\"\r\nContent-Type: application/json\r\n\r\n");
            Append(body, RequestJson(model, options));
            Append(body, $"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"audio\"; filename=\"audio.wav\"\r\n" +
                         "Content-Type: audio/wav\r\n\r\n");
            body.Write(wav, 0, wav.Length);
            Append(body, $"\r\n--{boundary}--\r\n");
            return body.ToArray();
        }

        public static TranscriptEvent ParseResponse(string json)
        {
            string transcript = null;
            double audioMs = 0;
            try
            {
                using var reader = new JsonTextReader(new StringReader(json));
                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.PropertyName || reader.Depth != 1)
                        continue;
                    var name = (string)reader.Value;
                    reader.Read();
                    if (name == "transcript")
                        transcript = reader.Value as string;
                    else if (name == "audioDurationMs" && reader.Value != null)
                        audioMs = Convert.ToDouble(reader.Value);
                    reader.Skip();
                }
            }
            catch (JsonException e)
            {
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Protocol, "Unreadable transcribe response.", inner: e);
            }

            if (transcript == null)
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Protocol, "Transcribe response has no transcript.");
            return new TranscriptEvent(TranscriptKind.Final, TranscriptText.Normalize(transcript), audioMs);
        }

        static string RequestJson(string model, SttOptions options)
        {
            var sb = new StringBuilder(128);
            using (var w = new JsonTextWriter(new StringWriter(sb)))
            {
                w.WriteStartObject();
                w.WritePropertyName("model");
                w.WriteValue(model);
                w.WritePropertyName("audioEncoding");
                w.WriteValue("WAV");
                if (!string.IsNullOrEmpty(options.LanguageBias))
                {
                    w.WritePropertyName("languageBias");
                    w.WriteStartArray();
                    w.WriteValue(options.LanguageBias);
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            return sb.ToString();
        }

        static void Append(Stream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
