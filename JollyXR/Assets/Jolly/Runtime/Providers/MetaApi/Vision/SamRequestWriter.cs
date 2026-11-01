using System.Globalization;
using System.IO;
using System.Text;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Common;
using Newtonsoft.Json;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// Streaming <c>POST /v1/responses</c> body for SAM 3.1 (meta-model-api.md §4.1): the output text arrives about
    /// 150 ms before <c>response.completed</c> (§4.2). <c>one_bit</c> is requested explicitly because point and box
    /// prompts returned lossless masks without it (live, 2026-11-01).
    /// </summary>
    static class SamRequestWriter
    {
        public static string Write(string model, string promptText, EncodedImage image)
        {
            var dataUrl = ImageDataUrl.Of(image);
            var sb = new StringBuilder(dataUrl.Length + promptText.Length + 384);
            using (var text = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (var w = new JsonTextWriter(text))
            {
                w.WriteStartObject();
                w.WritePropertyName("model"); w.WriteValue(model);
                w.WritePropertyName("stream"); w.WriteValue(true);
                w.WritePropertyName("store"); w.WriteValue(false);
                w.WritePropertyName("metadata");
                w.WriteStartObject();
                w.WritePropertyName("include_confidence"); w.WriteValue("true");
                w.WritePropertyName("mask_encoding"); w.WriteValue("one_bit");
                w.WriteEndObject();
                w.WritePropertyName("input");
                w.WriteStartArray();
                w.WriteStartObject();
                w.WritePropertyName("type"); w.WriteValue("message");
                w.WritePropertyName("role"); w.WriteValue("user");
                w.WritePropertyName("content");
                w.WriteStartArray();
                w.WriteStartObject();
                w.WritePropertyName("type"); w.WriteValue("input_text");
                w.WritePropertyName("text"); w.WriteValue(promptText);
                w.WriteEndObject();
                w.WriteStartObject();
                w.WritePropertyName("type"); w.WriteValue("input_image");
                w.WritePropertyName("image_url"); w.WriteValue(dataUrl);
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return sb.ToString();
        }
    }
}
