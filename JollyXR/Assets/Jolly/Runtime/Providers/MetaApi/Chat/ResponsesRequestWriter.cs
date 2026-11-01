using System;
using System.Globalization;
using System.IO;
using System.Text;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Common;
using Newtonsoft.Json;

namespace Jolly.Providers.MetaApi.Chat
{
    public readonly struct ResponsesRequestBody
    {
        public ResponsesRequestBody(string json, int inputChars) { Json = json; InputChars = inputChars; }
        public string Json { get; }
        /// <summary>Characters of billable input (text, tool schemas, a fixed cost per image) for the abort estimate.</summary>
        public int InputChars { get; }
    }

    /// <summary>
    /// Writes POST /v1/responses bodies (ADR-0004) with JsonTextWriter into a reused StringBuilder; no reflection.
    /// Within a turn Opaque items (reasoning with encrypted_content) are replayed verbatim; function calls are written
    /// without their server item id (verified live 2026-11-01: round 2 accepted, cached_tokens 497).
    /// </summary>
    public sealed class ResponsesRequestWriter
    {
        /// <summary>A 640 px JPEG costs about 300 input tokens (meta-model-api.md §3), i.e. 1,200 chars at 4 chars/token.</summary>
        public const int ImageInputChars = 1200;

        readonly StringBuilder _sb = new(8 * 1024);
        int _inputChars;

        public ResponsesRequestBody Write(ModelRequest request, string model)
        {
            _sb.Clear();
            _inputChars = 0;
            using (var w = new JsonTextWriter(new StringWriter(_sb, CultureInfo.InvariantCulture)))
            {
                w.WriteStartObject();
                w.WritePropertyName("model");
                w.WriteValue(model);
                w.WritePropertyName("stream");
                w.WriteValue(true);
                w.WritePropertyName("store");
                w.WriteValue(false);
                if (request.Instructions != null)
                {
                    w.WritePropertyName("instructions");
                    w.WriteValue(Count(request.Instructions));
                }
                WriteInput(w, request);
                WriteTools(w, request);
                w.WritePropertyName("parallel_tool_calls");
                w.WriteValue(true);
                w.WritePropertyName("reasoning");
                w.WriteStartObject();
                w.WritePropertyName("effort");
                w.WriteValue(EffortName(request.Effort));
                w.WriteEndObject();
                w.WritePropertyName("include");
                w.WriteStartArray();
                w.WriteValue("reasoning.encrypted_content");
                w.WriteEndArray();
                w.WritePropertyName("max_output_tokens");
                w.WriteValue(request.MaxOutputTokens);
                if (request.SafetyIdentifier != null)
                {
                    w.WritePropertyName("safety_identifier");
                    w.WriteValue(request.SafetyIdentifier);
                }
                w.WriteEndObject();
            }
            return new ResponsesRequestBody(_sb.ToString(), _inputChars);
        }

        void WriteInput(JsonWriter w, ModelRequest request)
        {
            w.WritePropertyName("input");
            w.WriteStartArray();
            if (request.Input != null)
            {
                foreach (var item in request.Input)
                    WriteItem(w, item);
            }
            w.WriteEndArray();
        }

        void WriteItem(JsonWriter w, ConversationItem item)
        {
            switch (item.Kind)
            {
                case ItemKind.UserText:
                    WriteMessage(w, "user", "input_text", "text", Count(item.Text));
                    break;
                case ItemKind.UserImage:
                    _inputChars += ImageInputChars;
                    WriteMessage(w, "user", "input_image", "image_url", ImageDataUrl.Of(item.Image));
                    break;
                case ItemKind.AssistantText:
                    WriteMessage(w, "assistant", "output_text", "text", Count(item.Text));
                    break;
                case ItemKind.ToolCall:
                    w.WriteStartObject();
                    WriteString(w, "type", "function_call");
                    WriteString(w, "call_id", item.CallId);
                    WriteString(w, "name", item.ToolName);
                    WriteString(w, "arguments", Count(item.Json));
                    w.WriteEndObject();
                    break;
                case ItemKind.ToolResult:
                    w.WriteStartObject();
                    WriteString(w, "type", "function_call_output");
                    WriteString(w, "call_id", item.CallId);
                    WriteString(w, "output", Count(item.Json));
                    w.WriteEndObject();
                    break;
                case ItemKind.Opaque:
                    w.WriteRawValue(Count(item.Json));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Unknown conversation item kind.");
            }
        }

        void WriteTools(JsonWriter w, ModelRequest request)
        {
            if (request.Tools == null || request.Tools.Count == 0)
                return;
            w.WritePropertyName("tools");
            w.WriteStartArray();
            foreach (var tool in request.Tools)
            {
                w.WriteStartObject();
                WriteString(w, "type", "function");
                WriteString(w, "name", tool.Name);
                WriteString(w, "description", Count(tool.Description));
                w.WritePropertyName("parameters");
                w.WriteRawValue(Count(tool.ParametersJsonSchema));
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }

        static void WriteMessage(JsonWriter w, string role, string partType, string valueName, string value)
        {
            w.WriteStartObject();
            WriteString(w, "type", "message");
            WriteString(w, "role", role);
            w.WritePropertyName("content");
            w.WriteStartArray();
            w.WriteStartObject();
            WriteString(w, "type", partType);
            WriteString(w, valueName, value);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteEndObject();
        }

        static void WriteString(JsonWriter w, string name, string value)
        {
            w.WritePropertyName(name);
            w.WriteValue(value);
        }

        string Count(string text)
        {
            _inputChars += text?.Length ?? 0;
            return text;
        }

        static string EffortName(ReasoningEffort effort) => effort switch
        {
            ReasoningEffort.Minimal => "minimal",
            ReasoningEffort.Low => "low",
            ReasoningEffort.Medium => "medium",
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, null),
        };
    }
}
