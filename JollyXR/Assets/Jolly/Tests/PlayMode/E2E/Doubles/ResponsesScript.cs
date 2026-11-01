using System.Collections.Generic;
using Jolly.Net.Sse;
using Newtonsoft.Json.Linq;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>
    /// Responses API event streams in the shape recorded live (Tests/Fixtures/sse): created, text deltas, items done,
    /// completed with usage, then [DONE]. Only the events the reducer reads are produced.
    /// </summary>
    public sealed class ResponsesScript
    {
        readonly List<SseEvent> _events = new();
        int _calls;

        public ResponsesScript() => _events.Add(Event("response.created",
            new JObject { ["response"] = new JObject { ["id"] = "resp_e2e", ["status"] = "in_progress" } }));

        public ResponsesScript Say(params string[] deltas)
        {
            var text = string.Concat(deltas);
            foreach (var d in deltas)
                _events.Add(Event("response.output_text.delta", new JObject { ["delta"] = d }));
            _events.Add(Event("response.output_item.done", new JObject
            {
                ["item"] = new JObject
                {
                    ["type"] = "message", ["role"] = "assistant",
                    ["content"] = new JArray(new JObject { ["type"] = "output_text", ["text"] = text }),
                },
            }));
            return this;
        }

        public ResponsesScript Call(string name, string argumentsJson)
        {
            _events.Add(Event("response.output_item.done", new JObject
            {
                ["item"] = new JObject
                {
                    ["type"] = "function_call", ["call_id"] = "call_e2e_" + ++_calls, ["name"] = name,
                    ["arguments"] = argumentsJson,
                },
            }));
            return this;
        }

        public List<SseEvent> Complete()
        {
            var events = new List<SseEvent>(_events)
            {
                Event("response.completed", new JObject
                {
                    ["response"] = new JObject
                    {
                        ["id"] = "resp_e2e", ["status"] = "completed",
                        ["usage"] = new JObject { ["input_tokens"] = 900, ["output_tokens"] = 40 },
                    },
                }),
                new SseEvent(null, SseEvent.DoneSentinel),
            };
            return events;
        }

        /// <summary>A SAM stream around a recorded non-streaming body (Tests/Fixtures/sam).</summary>
        public static List<SseEvent> Sam(string recordedResponseJson)
        {
            var response = JObject.Parse(recordedResponseJson);
            var text = (string)response.SelectToken("output[0].content[0].text") ?? string.Empty;
            return new List<SseEvent>
            {
                Event("response.created", new JObject { ["response"] = new JObject { ["id"] = (string)response["id"] } }),
                Event("response.output_text.delta", new JObject { ["content_index"] = 0, ["delta"] = text }),
                Event("response.completed", new JObject { ["response"] = response }),
                new SseEvent(null, SseEvent.DoneSentinel),
            };
        }

        static SseEvent Event(string type, JObject payload)
        {
            payload["type"] = type;
            return new SseEvent(type, payload.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
