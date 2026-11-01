using System.Text;
using Jolly.Net.Sse;
using Jolly.Providers.MetaApi.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// Folds a streamed SAM 3.1 response (meta-model-api.md §4.2). A still produces its whole output in one text part,
    /// so <c>response.output_text.done</c> finishes the read about 150 ms before <c>response.completed</c>; a no-match
    /// sends no text events and finishes on <c>response.completed</c>. response.failed and "error" throw ProviderException.
    /// </summary>
    sealed class SamStreamReader
    {
        readonly StringBuilder _text = new();

        public string ResponseId { get; private set; }
        public bool IsFinished { get; private set; }
        public string OutputText => _text.ToString();
        /// <summary>1 unless response.completed reported otherwise: one request carries one still.</summary>
        public int ImagesProcessed { get; private set; } = 1;

        /// <returns>True once the output is known and the rest of the stream can be dropped.</returns>
        public bool Apply(in SseEvent sse)
        {
            if (IsFinished)
                return true;
            if (sse.IsDone)
                throw MetaErrors.Protocol("SAM stream ended before its output");
            if (sse.EventName != null && !IsHandled(sse.EventName))
                return false;

            var json = Parse(sse.Data);
            switch ((string)json["type"] ?? sse.EventName)
            {
                case "response.created":
                    ResponseId = (string)json.SelectToken("response.id");
                    break;
                case "response.output_text.delta":
                    _text.Append((string)json["delta"]);
                    break;
                case "response.output_text.done":
                    if (json["text"] is JValue { Type: JTokenType.String } done)
                        _text.Clear().Append((string)done);
                    IsFinished = true;
                    break;
                case "response.completed":
                    ImagesProcessed = (int?)json.SelectToken("response.usage.images_processed") ?? 1;
                    IsFinished = true;
                    break;
                case "response.incomplete":
                    throw MetaErrors.Protocol("SAM response incomplete: " + (string)json.SelectToken("response.incomplete_details.reason"));
                case "response.failed":
                    var error = json.SelectToken("response.error");
                    throw MetaErrors.FromStreamError((string)error?["code"], (string)error?["message"], ResponseId);
                case "error":
                    throw MetaErrors.FromStreamError((string)json["code"], (string)json["message"], ResponseId);
            }
            return IsFinished;
        }

        static bool IsHandled(string eventName) => eventName switch
        {
            "response.created" or "response.output_text.delta" or "response.output_text.done" or "response.completed"
                or "response.incomplete" or "response.failed" or "error" => true,
            _ => false,
        };

        static JObject Parse(string data)
        {
            try
            {
                return JObject.Parse(data);
            }
            catch (JsonException e)
            {
                throw MetaErrors.Protocol("malformed SAM stream event: " + e.Message);
            }
        }
    }
}
