using System;
using System.Collections.Generic;
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
        public const int MaxBuiltInToolCalls = 2;

        readonly StringBuilder _sb = new(8 * 1024);
        int _inputChars;

        /// <summary>
        /// Fields are written in prompt order (instructions, tools, input) with nothing per-request ahead of the input, so
        /// two requests that share a prompt prefix also share a body prefix (architecture §7).
        /// </summary>
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
                WriteString(w, "prompt_cache_key", PromptCacheKey.For(model, request));
                if (request.Instructions != null)
                {
                    w.WritePropertyName("instructions");
                    w.WriteValue(Count(request.Instructions));
                }
                WriteTools(w, request);
                WriteInput(w, request);
                w.WritePropertyName("parallel_tool_calls");
                w.WriteValue(true);
                if (request.WebSearch != null)
                {
                    // Counts built-in tools only (web_search, tool_search), never our functions; 2 lets a follow-up
                    // search refine the first without a long chain of them.
                    w.WritePropertyName("max_tool_calls");
                    w.WriteValue(MaxBuiltInToolCalls);
                }
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

        /// <summary>
        /// Top-level functions, then one namespace object per <see cref="ModelRequest.Namespaces"/> entry holding its
        /// "ns.fn" tools as "fn" (eager: no defer_loading, so no tool_search round, ADR-0047), then web_search. Each group
        /// keeps the order of <see cref="ModelRequest.Tools"/>, so the body prefix is as stable as the tool list. A dotted
        /// name whose namespace is not in the request goes out flat, which the API also accepts (one dot at most).
        /// </summary>
        void WriteTools(JsonWriter w, ModelRequest request)
        {
            var tools = request.Tools ?? Array.Empty<ToolDefinition>();
            if (tools.Count == 0 && request.WebSearch == null)
                return;
            w.WritePropertyName("tools");
            w.WriteStartArray();
            foreach (var tool in tools)
            {
                if (NamespaceOf(tool.Name, request.Namespaces) == null)
                    WriteFunction(w, tool, tool.Name);
            }
            if (request.Namespaces != null)
            {
                foreach (var ns in request.Namespaces)
                    WriteNamespace(w, ns, tools);
            }
            if (request.WebSearch != null)
                WriteWebSearch(w, request.WebSearch);
            w.WriteEndArray();
        }

        void WriteNamespace(JsonWriter w, ToolNamespace ns, IReadOnlyList<ToolDefinition> tools)
        {
            var prefix = ns.Name + ".";
            var opened = false;
            foreach (var tool in tools)
            {
                if (!tool.Name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (!opened)
                {
                    opened = true;
                    w.WriteStartObject();
                    WriteString(w, "type", "namespace");
                    WriteString(w, "name", ns.Name);
                    WriteString(w, "description", Count(ns.Description));
                    w.WritePropertyName("tools");
                    w.WriteStartArray();
                }
                WriteFunction(w, tool, tool.Name.Substring(prefix.Length));
            }
            if (!opened)
                return;
            w.WriteEndArray();
            w.WriteEndObject();
        }

        static ToolNamespace NamespaceOf(string toolName, IReadOnlyList<ToolNamespace> namespaces)
        {
            var dot = toolName.IndexOf('.');
            if (dot < 0 || namespaces == null)
                return null;
            foreach (var ns in namespaces)
            {
                if (ns.Name.Length == dot && string.CompareOrdinal(toolName, 0, ns.Name, 0, dot) == 0)
                    return ns;
            }
            return null;
        }

        void WriteFunction(JsonWriter w, ToolDefinition tool, string name)
        {
            w.WriteStartObject();
            WriteString(w, "type", "function");
            WriteString(w, "name", name);
            WriteString(w, "description", Count(tool.Description));
            w.WritePropertyName("parameters");
            w.WriteRawValue(Count(tool.ParametersJsonSchema));
            w.WriteEndObject();
        }

        /// <summary>"low" context: the answer is one or two spoken sentences, and more context only adds input tokens
        /// (about 3,000 per search at low, live 2026-11-02). Only the location fields that are set are sent.</summary>
        static void WriteWebSearch(JsonWriter w, WebSearchOptions search)
        {
            w.WriteStartObject();
            WriteString(w, "type", "web_search");
            WriteString(w, "search_context_size", "low");
            w.WritePropertyName("user_location");
            w.WriteStartObject();
            WriteString(w, "type", "approximate");
            WriteOptional(w, "country", search.Country);
            WriteOptional(w, "region", search.Region);
            WriteOptional(w, "city", search.City);
            WriteOptional(w, "timezone", search.Timezone);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        static void WriteOptional(JsonWriter w, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
                WriteString(w, name, value);
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
