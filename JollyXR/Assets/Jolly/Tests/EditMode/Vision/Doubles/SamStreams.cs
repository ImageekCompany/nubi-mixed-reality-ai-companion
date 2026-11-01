using System.Collections.Generic;
using Jolly.Net.Sse;
using Newtonsoft.Json.Linq;

namespace Jolly.Tests.EditMode.Vision.Doubles
{
    /// <summary>
    /// SAM SSE event sequences in the order and shape recorded live in meta-model-api.md §4.2. The recorded fixtures are
    /// non-streaming bodies, which are the same response object that <c>response.completed</c> carries.
    /// </summary>
    static class SamStreams
    {
        public static List<SseEvent> FromRecorded(string responseJson)
        {
            var response = JObject.Parse(responseJson);
            var text = (string)response.SelectToken("output[0].content[0].text") ?? "";
            return Head((string)response["id"], text, withDone: true, Completed(response));
        }

        /// <summary>Output text in one delta and its done echo, then a completed response with images_processed:1.</summary>
        public static List<SseEvent> WithText(string text) =>
            Head("resp_test", text, withDone: true, Completed(CompletedResponse("resp_test")));

        public static List<SseEvent> UpToOutputTextDone(string text) => Head("resp_test", text, withDone: true);

        /// <summary>A delta but no output_text.done: the text must still be taken from response.completed.</summary>
        public static List<SseEvent> DeltaThenCompleted(string text) =>
            Head("resp_test", text, withDone: false, Completed(CompletedResponse("resp_test")));

        public static List<SseEvent> Failed(string code) => new()
        {
            Created("resp_test"),
            Event("response.failed", new JObject
            {
                ["response"] = new JObject { ["id"] = "resp_test", ["status"] = "failed",
                    ["error"] = new JObject { ["code"] = code, ["message"] = "backend unavailable" } },
            }),
        };

        public static List<SseEvent> EndingBeforeOutput() => new() { Created("resp_test"), new SseEvent(null, SseEvent.DoneSentinel) };

        static List<SseEvent> Head(string id, string text, bool withDone, params SseEvent[] tail)
        {
            var events = new List<SseEvent> { Created(id), Event("response.in_progress", new JObject()) };
            if (text.Length > 0)
            {
                events.Add(Event("response.output_text.delta", new JObject { ["content_index"] = 0, ["delta"] = text }));
                if (withDone)
                    events.Add(Event("response.output_text.done", new JObject { ["content_index"] = 0, ["text"] = text }));
            }
            events.AddRange(tail);
            if (tail.Length > 0)
                events.Add(new SseEvent(null, SseEvent.DoneSentinel));
            return events;
        }

        static SseEvent Created(string id) =>
            Event("response.created", new JObject { ["response"] = new JObject { ["id"] = id, ["status"] = "in_progress" } });

        static SseEvent Completed(JObject response) => Event("response.completed", new JObject { ["response"] = response });

        static JObject CompletedResponse(string id) => new()
        {
            ["id"] = id,
            ["status"] = "completed",
            ["usage"] = new JObject { ["images_processed"] = 1, ["input_tokens"] = 0, ["output_tokens"] = 0 },
        };

        static SseEvent Event(string type, JObject data)
        {
            data["type"] = type;
            return new SseEvent(type, data.ToString(Newtonsoft.Json.Formatting.None));
        }
    }
}
