using System;
using System.Collections.Generic;
using System.IO;
using Jolly.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Core.Memory
{
    /// <summary>Persistence DTO for one conversation item; public fields only (ADR-0015).</summary>
    public sealed class ConversationItemDto
    {
        public string kind;
        public string text;
        public string call_id;
        public string name;
        public string json;

        public static ConversationItemDto From(ConversationItem item) => new()
        {
            kind = item.Kind.ToString(), text = item.Text, call_id = item.CallId, name = item.ToolName, json = item.Json,
        };

        public ConversationItem ToItem() => kind switch
        {
            nameof(ItemKind.UserText) => ConversationItem.User(text),
            nameof(ItemKind.AssistantText) => ConversationItem.Assistant(text),
            nameof(ItemKind.ToolCall) => ConversationItem.Call(call_id, name, json),
            nameof(ItemKind.ToolResult) => ConversationItem.Result(call_id, json),
            _ => throw new FormatException($"unsupported item kind '{kind}'"),
        };
    }

    /// <summary>Hand-written JSON codec of <c>conversation.&lt;persona&gt;.json</c>: {version, summary, recent[]}.</summary>
    public static class ConversationFile
    {
        const int Version = 1;

        public static string Write(string summary, IReadOnlyList<ConversationItem> recent)
        {
            var text = new StringWriter();
            using (var w = new JsonTextWriter(text))
            {
                w.WriteStartObject();
                w.WritePropertyName("version"); w.WriteValue(Version);
                w.WritePropertyName("summary"); w.WriteValue(summary ?? string.Empty);
                w.WritePropertyName("recent");
                w.WriteStartArray();
                foreach (var item in recent)
                {
                    var dto = ConversationItemDto.From(item);
                    w.WriteStartObject();
                    Field(w, "kind", dto.kind);
                    Field(w, "text", dto.text);
                    Field(w, "call_id", dto.call_id);
                    Field(w, "name", dto.name);
                    Field(w, "json", dto.json);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return text.ToString();
        }

        /// <summary>Throws <see cref="FormatException"/> or <see cref="JsonException"/> for a malformed file.</summary>
        public static string Read(string json, List<ConversationItem> into)
        {
            var root = JObject.Parse(json);
            if ((int?)root["version"] != Version)
                throw new FormatException("unsupported conversation file version");
            foreach (var token in (JArray)root["recent"] ?? new JArray())
            {
                var dto = new ConversationItemDto
                {
                    kind = (string)token["kind"], text = (string)token["text"], call_id = (string)token["call_id"],
                    name = (string)token["name"], json = (string)token["json"],
                };
                into.Add(dto.ToItem());
            }
            return (string)root["summary"] ?? string.Empty;
        }

        static void Field(JsonWriter w, string name, string value)
        {
            if (value == null)
                return;
            w.WritePropertyName(name);
            w.WriteValue(value);
        }
    }
}
