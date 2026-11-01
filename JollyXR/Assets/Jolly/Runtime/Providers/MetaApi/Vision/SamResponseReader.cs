using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>The fields of a non-streaming Responses object that SAM needs.</summary>
    readonly struct SamResponse
    {
        public SamResponse(string id, string status, string errorMessage, string outputText, int imagesProcessed)
        { Id = id; Status = status; ErrorMessage = errorMessage; OutputText = outputText; ImagesProcessed = imagesProcessed; }

        public string Id { get; }
        public string Status { get; }
        public string ErrorMessage { get; }
        /// <summary>Every <c>output_text</c> part of every message item, concatenated; empty when nothing matched.</summary>
        public string OutputText { get; }
        /// <summary>Billed images; 1 when the response omits usage, because the request reached SAM.</summary>
        public int ImagesProcessed { get; }
        public bool IsCompleted => Status == "completed";
    }

    static class SamResponseReader
    {
        /// <exception cref="FormatException">The body is not a Responses object.</exception>
        public static SamResponse Read(string json)
        {
            try
            {
                var root = JObject.Parse(json);
                var status = (string)root["status"] ?? throw new FormatException("SAM response has no status.");
                var error = root["error"] as JObject;
                var usage = root["usage"] as JObject;
                return new SamResponse((string)root["id"], status, (string)error?["message"], OutputText(root),
                    (int?)usage?["images_processed"] ?? 1);
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is InvalidCastException)
            {
                throw new FormatException("SAM response is not a Responses object.", e);
            }
        }

        static string OutputText(JObject root)
        {
            var text = new StringBuilder();
            if (root["output"] is not JArray output)
                return "";
            foreach (var item in output)
            {
                if (item is not JObject message || (string)message["type"] != "message" || message["content"] is not JArray parts)
                    continue;
                foreach (var part in parts)
                {
                    if (part is JObject p && (string)p["type"] == "output_text")
                        text.Append((string)p["text"]);
                }
            }
            return text.ToString();
        }
    }
}
