using System.IO;
using System.Text;
using Jolly.Contracts;
using Newtonsoft.Json;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Client frames of <c>wss://api.meta.ai/v1/asr/realtime</c> (meta-model-api.md §5.2). The HTTP Authorization header
    /// is ignored by the server; the key travels in this first text frame. <c>keywords</c> is never sent: biasing
    /// duplicated "Jolly" in a live test (ADR-0005).
    /// </summary>
    public static class MuseAsrHandshake
    {
        public const string EndStream = "{\"type\":\"endStream\"}";

        public static string Build(string apiKey, string model, SttOptions options)
        {
            var encoding = EncodingFor(options.SampleRate);
            var sb = new StringBuilder(256);
            using (var writer = new JsonTextWriter(new StringWriter(sb)))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("authorization");
                writer.WriteStartObject();
                writer.WritePropertyName("accessToken");
                writer.WriteValue("Bearer " + apiKey);
                writer.WriteEndObject();
                writer.WritePropertyName("audioEncoding");
                writer.WriteValue(encoding);
                writer.WritePropertyName("model");
                writer.WriteValue(model);
                writer.WritePropertyName("mode");
                writer.WriteValue("PUSH_TO_TALK");
                writer.WritePropertyName("partialMode");
                writer.WriteValue("CUMULATIVE");
                writer.WritePropertyName("emitAudioProgress");
                writer.WriteValue(false);
                if (!string.IsNullOrEmpty(options.LanguageBias))
                {
                    writer.WritePropertyName("languageBias");
                    writer.WriteStartArray();
                    writer.WriteValue(options.LanguageBias);
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            return sb.ToString();
        }

        static string EncodingFor(int sampleRate) => sampleRate switch
        {
            24000 => "PCM_24KHZ",
            16000 => "PCM_16KHZ",
            _ => throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.BadRequest,
                $"Muse ASR accepts 16000 or 24000 Hz, not {sampleRate}."),
        };
    }
}
