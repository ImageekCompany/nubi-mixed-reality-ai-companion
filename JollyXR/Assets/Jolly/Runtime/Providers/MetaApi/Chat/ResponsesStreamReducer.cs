using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jolly.Contracts;
using Jolly.Net.Sse;
using Jolly.Providers.MetaApi.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Providers.MetaApi.Chat
{
    /// <summary>
    /// Folds one Responses API event stream (architecture §7) into ModelEvents. Event names are used exactly as
    /// observed live; unknown events are ignored. response.failed and "error" throw ProviderException.
    /// response.incomplete is a normal completion with FinishReason Length/Incomplete: its text may already be spoken.
    /// </summary>
    public sealed class ResponsesStreamReducer
    {
        readonly string _model;
        readonly List<ConversationItem> _items = new(4);
        string _responseId;
        bool _started;
        bool _hasCalls;

        public ResponsesStreamReducer(string model) => _model = model;

        public bool IsCompleted { get; private set; }
        /// <summary>Characters of text and argument deltas seen, for the abort estimate.</summary>
        public int OutputChars { get; private set; }

        public void Apply(in SseEvent sse, List<ModelEvent> output)
        {
            if (sse.IsDone)
            {
                if (!IsCompleted)
                    throw MetaErrors.Protocol("stream ended before response.completed");
                return;
            }
            if (IsCompleted)
                return;
            if (!_started)
            {
                _started = true;
                output.Add(new ModelEvent(ModelEventKind.Started));
            }
            // Most events (in_progress, content_part.*, *.done echoes) carry nothing new; skip parsing their JSON.
            if (sse.EventName != null && !IsHandled(sse.EventName))
                return;

            var json = IsEnvelope(sse.EventName) ? ParseEnvelope(sse.Data) : Parse(sse.Data);
            try
            {
                Reduce(json, sse.EventName, output);
            }
            catch (Exception e) when (e is ArgumentException or InvalidCastException or InvalidOperationException
                                          or FormatException or OverflowException)
            {
                // A field of an unexpected JSON type: JToken casts and indexers throw these, not JsonException.
                throw MetaErrors.Protocol("malformed stream event: " + e.Message);
            }
        }

        void Reduce(JObject json, string eventName, List<ModelEvent> output)
        {
            switch ((string)json["type"] ?? eventName)
            {
                case "response.created":
                    _responseId = (string)json.SelectToken("response.id");
                    break;
                case "response.output_text.delta":
                    var delta = (string)json["delta"];
                    if (!string.IsNullOrEmpty(delta))
                    {
                        OutputChars += delta.Length;
                        output.Add(new ModelEvent(ModelEventKind.TextDelta, text: delta));
                    }
                    break;
                case "response.function_call_arguments.delta":
                    OutputChars += ((string)json["delta"])?.Length ?? 0;
                    break;
                case "response.output_item.done":
                    OnItemDone(json["item"] as JObject, output);
                    break;
                case "response.completed":
                    Complete(json["response"] as JObject, _hasCalls ? FinishReason.ToolCalls : FinishReason.Stop, output);
                    break;
                case "response.incomplete":
                    var reason = (string)json.SelectToken("response.incomplete_details.reason");
                    Complete(json["response"] as JObject, reason == "max_output_tokens" ? FinishReason.Length : FinishReason.Incomplete, output);
                    break;
                case "response.failed":
                    var error = json.SelectToken("response.error") as JObject;
                    throw MetaErrors.FromStreamError((string)error?["code"], (string)error?["message"], _responseId);
                case "error":
                    throw MetaErrors.FromStreamError((string)json["code"], (string)json["message"], _responseId);
            }
        }

        static bool IsHandled(string eventName) => eventName switch
        {
            "response.created" or "response.output_text.delta" or "response.function_call_arguments.delta"
                or "response.output_item.done" or "response.completed" or "response.incomplete" or "response.failed"
                or "error" => true,
            _ => false,
        };

        /// <summary>Events whose payload is the whole response object, which echoes the instructions, tools and output.</summary>
        static bool IsEnvelope(string eventName) => eventName is "response.created" or "response.completed"
            or "response.incomplete" or "response.failed";

        /// <summary>
        /// Reads only what <see cref="Reduce"/> uses of a response envelope (its type and the response's id, usage,
        /// incomplete_details and error) and skips the echoed request: the full tree of a 10–20 KB echo was the
        /// largest allocation of the Responses path in the Quest allocation profiles (ADR-0023).
        /// </summary>
        static JObject ParseEnvelope(string data)
        {
            try
            {
                using var reader = new JsonTextReader(new StringReader(data));
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new JsonReaderException("Expected a JSON object.");
                var envelope = new JObject();
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    var name = (string)reader.Value;
                    reader.Read();
                    if (name == "response" && reader.TokenType == JsonToken.StartObject)
                        envelope[name] = ReadResponseFields(reader);
                    else if (name is "type" or "response")
                        envelope[name] = JToken.ReadFrom(reader);
                    else
                        reader.Skip();
                }
                return envelope;
            }
            catch (JsonException e)
            {
                throw MetaErrors.Protocol("malformed stream event: " + e.Message);
            }
        }

        static JObject ReadResponseFields(JsonTextReader reader)
        {
            var response = new JObject();
            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                var name = (string)reader.Value;
                reader.Read();
                if (name is "id" or "usage" or "incomplete_details" or "error")
                    response[name] = JToken.ReadFrom(reader);
                else
                    reader.Skip();
            }
            return response;
        }

        void OnItemDone(JObject item, List<ModelEvent> output)
        {
            switch ((string)item?["type"])
            {
                case null:
                    return;
                case "function_call":
                    var call = new ToolCallRequest((string)item["call_id"], (string)item["name"], (string)item["arguments"] ?? "{}");
                    _hasCalls = true;
                    _items.Add(ConversationItem.Call(call.CallId, call.Name, call.ArgumentsJson));
                    output.Add(new ModelEvent(ModelEventKind.ToolCall, toolCall: call));
                    return;
                case "message":
                    var text = MessageText(item);
                    if (text.Length > 0)
                        _items.Add(ConversationItem.Assistant(text));
                    return;
                default:
                    // reasoning (with encrypted_content) and anything newer: replayed verbatim within the turn.
                    _items.Add(ConversationItem.Opaque(item.ToString(Formatting.None)));
                    return;
            }
        }

        void Complete(JObject response, FinishReason finish, List<ModelEvent> output)
        {
            var usage = response?["usage"] as JObject;
            IsCompleted = true;
            output.Add(new ModelEvent(ModelEventKind.Completed, result: new ModelTurnResult
            {
                FinishReason = finish,
                ResponseId = (string)response?["id"] ?? _responseId,
                OutputItems = _items.ToArray(),
                Usage = new UsageRecord(_model,
                    (long?)usage?["input_tokens"] ?? 0,
                    (long?)usage?.SelectToken("input_tokens_details.cached_tokens") ?? 0,
                    (long?)usage?["output_tokens"] ?? 0),
            }));
        }

        static string MessageText(JObject item)
        {
            if (item["content"] is not JArray parts)
                return string.Empty;
            var text = new StringBuilder();
            foreach (var part in parts)
            {
                if ((string)part["type"] == "output_text")
                    text.Append((string)part["text"]);
            }
            return text.ToString();
        }

        static JObject Parse(string data)
        {
            try
            {
                return JObject.Parse(data);
            }
            catch (JsonException e)
            {
                throw MetaErrors.Protocol("malformed stream event: " + e.Message);
            }
        }
    }
}
