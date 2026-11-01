using System;
using System.IO;
using Jolly.Contracts;
using Newtonsoft.Json;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Parses one realtime server frame with a forward-only reader (ADR-0015: no reflection on hot paths). Unknown
    /// properties and nested values are skipped, so new server fields do not break the client.
    /// </summary>
    public static class MuseAsrEventParser
    {
        public static MuseAsrEvent Parse(string json)
        {
            try
            {
                return ParseObject(json);
            }
            catch (JsonException e)
            {
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Protocol,
                    "Unreadable ASR frame: " + e.Message, inner: e);
            }
        }

        static MuseAsrEvent ParseObject(string json)
        {
            string type = null, sessionId = null, text = null, message = null, errorType = null, errorCode = null, errorParam = null;
            var final = false;
            double audioMs = 0;

            using var reader = new JsonTextReader(new StringReader(json));
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                throw new JsonReaderException("Expected a JSON object.");

            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                var name = (string)reader.Value;
                reader.Read();
                switch (name)
                {
                    case "type": type = reader.Value as string; break;
                    case "sessionId": sessionId = reader.Value as string; break;
                    case "transcript": text = reader.Value as string; break;
                    case "final": final = reader.Value is true; break;
                    case "audioProcessedMs": audioMs = reader.Value == null ? 0 : Convert.ToDouble(reader.Value); break;
                    case "message": message = reader.Value as string; break;
                    case "errorType": errorType = reader.Value as string; break;
                    case "errorCode": errorCode = reader.Value?.ToString(); break;
                    case "errorParam": errorParam = reader.Value as string; break;
                }
                reader.Skip();
            }
            if (reader.TokenType != JsonToken.EndObject)
                throw new JsonReaderException("Unterminated JSON object.");

            var kind = type switch
            {
                "transcript" => MuseAsrEventKind.Transcript,
                "error" => MuseAsrEventKind.Error,
                null when sessionId != null => MuseAsrEventKind.Ack,
                _ => MuseAsrEventKind.Other,
            };
            return new MuseAsrEvent(kind, sessionId, text ?? string.Empty, final, audioMs, message, errorType, errorCode, errorParam);
        }
    }
}
