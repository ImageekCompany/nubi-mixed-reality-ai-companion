using System.Collections.Generic;
using System.Linq;
using Jolly.Contracts;
using Jolly.Net.Sse;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Tests.EditMode.Net;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Chat
{
    public sealed class ResponsesStreamReducerTests
    {
        const string Model = "muse-spark-1.3-contributor";

        [Test]
        public void TextOnly_YieldsStartedDeltasAndCompletedWithUsage()
        {
            var events = Reduce("responses_text");

            Assert.That(events.First().Kind, Is.EqualTo(ModelEventKind.Started));
            Assert.That(events.Last().Kind, Is.EqualTo(ModelEventKind.Completed));
            Assert.That(Text(events), Is.EqualTo("Hey there — good to see you!"));
            Assert.That(events.Count(e => e.Kind == ModelEventKind.TextDelta), Is.EqualTo(4));

            var result = events.Last().Result;
            Assert.That(result.FinishReason, Is.EqualTo(FinishReason.Stop));
            Assert.That(result.ResponseId, Does.StartWith("resp_"));
            Assert.That(result.Usage.Model, Is.EqualTo(Model));
            Assert.That(result.Usage.InputTokens, Is.EqualTo(36));
            Assert.That(result.Usage.CachedInputTokens, Is.Zero);
            Assert.That(result.Usage.OutputTokens, Is.EqualTo(29));
        }

        [Test]
        public void TextOnly_OutputItemsAreReasoningThenAssistantText()
        {
            var items = Reduce("responses_text").Last().Result.OutputItems;

            Assert.That(items.Select(i => i.Kind), Is.EqualTo(new[] { ItemKind.Opaque, ItemKind.AssistantText }));
            Assert.That(items[0].Json, Does.Contain("\"type\":\"reasoning\"").And.Contain("encrypted_content"));
            Assert.That(items[1].Text, Is.EqualTo("Hey there — good to see you!"));
        }

        [Test]
        public void CommentaryThenFunctionCall_SpeaksThePreambleBeforeTheToolCall()
        {
            var events = Reduce("responses_commentary_tool");

            var kinds = events.Select(e => e.Kind).ToList();
            Assert.That(kinds.IndexOf(ModelEventKind.TextDelta), Is.LessThan(kinds.IndexOf(ModelEventKind.ToolCall)));
            Assert.That(Text(events), Is.EqualTo("On my way to the sofa."));

            var call = events.Single(e => e.Kind == ModelEventKind.ToolCall).ToolCall;
            Assert.That(call.Name, Is.EqualTo("walk_to"));
            Assert.That(call.CallId, Is.EqualTo("call_01a194ad50eb757280800296fbf06a9a"));
            Assert.That(call.ArgumentsJson, Is.EqualTo("{\"place\":\"sofa\"}"));

            var result = events.Last().Result;
            Assert.That(result.FinishReason, Is.EqualTo(FinishReason.ToolCalls));
            Assert.That(result.OutputItems.Select(i => i.Kind),
                Is.EqualTo(new[] { ItemKind.Opaque, ItemKind.AssistantText, ItemKind.ToolCall }));
            Assert.That(result.OutputItems[2].CallId, Is.EqualTo(call.CallId));
            Assert.That(result.OutputItems[2].Json, Is.EqualTo(call.ArgumentsJson));
        }

        [Test]
        public void MessageDone_EndsTheTextBeforeTheCallArgumentsStream()
        {
            // The preamble has no trailing whitespace, so without this it waited for the whole call (235 ms on the
            // Quest for a vision call, selftest-report.md 4d228c8).
            var kinds = Reduce("responses_commentary_tool").Select(e => e.Kind).ToList();

            Assert.That(kinds.Count(k => k == ModelEventKind.TextDone), Is.EqualTo(1));
            Assert.That(kinds.IndexOf(ModelEventKind.TextDone), Is.GreaterThan(kinds.LastIndexOf(ModelEventKind.TextDelta)));
            Assert.That(kinds.IndexOf(ModelEventKind.TextDone), Is.LessThan(kinds.IndexOf(ModelEventKind.ToolCall)));
        }

        [Test]
        public void ParallelCalls_EmitBothCallsInOrderWithDistinctIds()
        {
            var events = Reduce("responses_parallel_tools");

            var calls = events.Where(e => e.Kind == ModelEventKind.ToolCall).Select(e => e.ToolCall).ToList();
            Assert.That(calls.Select(c => c.Name), Is.EqualTo(new[] { "walk_to", "capture_and_identify" }));
            Assert.That(calls[0].CallId, Is.Not.EqualTo(calls[1].CallId));
            Assert.That(calls[1].ArgumentsJson, Is.EqualTo("{\"question\":\"What is this mug the user is holding?\"}"));
            Assert.That(events.Last().Result.Usage.OutputTokens, Is.EqualTo(177));
        }

        [Test]
        public void Incomplete_CompletesWithLengthFinishReason()
        {
            var result = Reduce("responses_incomplete").Last().Result;

            Assert.That(result.FinishReason, Is.EqualTo(FinishReason.Length));
            Assert.That(result.Usage.OutputTokens, Is.EqualTo(16));
        }

        [Test]
        public void ResponseFailed_ThrowsProviderExceptionWithTheServerCode()
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();

            var e = Assert.Throws<ProviderException>(() =>
            {
                foreach (var sse in SseFixtures.Parse("responses_failed"))
                    reducer.Apply(sse, output);
            });

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Server));
            Assert.That(e.Message, Does.Contain("server_error"));
            Assert.That(output.Select(x => x.Kind), Is.EqualTo(new[] { ModelEventKind.Started, ModelEventKind.TextDelta }));
        }

        [Test]
        public void ErrorEvent_ThrowsProviderExceptionMappedFromItsCode()
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();

            var e = Assert.Throws<ProviderException>(() =>
            {
                foreach (var sse in SseFixtures.Parse("responses_error_event"))
                    reducer.Apply(sse, output);
            });

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.RateLimited));
        }

        [Test]
        public void ResponseFailedWithNullError_ThrowsProviderException()
        {
            var reducer = new ResponsesStreamReducer(Model);

            var e = Assert.Throws<ProviderException>(() => reducer.Apply(
                new SseEvent("response.failed", "{\"type\":\"response.failed\",\"response\":{\"error\":null}}"), new List<ModelEvent>()));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Server));
        }

        [TestCase("{\"type\":\"response.completed\",\"response\":{\"id\":\"r\",\"usage\":null}}")]
        [TestCase("{\"type\":\"response.completed\",\"response\":null}")]
        [TestCase("{\"type\":\"response.completed\",\"response\":{\"id\":\"r\",\"usage\":{\"input_tokens_details\":null}}}")]
        public void CompletedWithNullUsage_CompletesWithZeroUsage(string data)
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();

            reducer.Apply(new SseEvent("response.completed", data), output);

            var usage = output.Last().Result.Usage;
            Assert.That(usage.InputTokens, Is.Zero);
            Assert.That(usage.CachedInputTokens, Is.Zero);
            Assert.That(usage.OutputTokens, Is.Zero);
        }

        [TestCase("{\"type\":\"response.output_text.delta\",\"delta\":{}}")]
        [TestCase("{\"type\":\"response.output_item.done\",\"item\":{\"type\":\"message\",\"content\":[1]}}")]
        [TestCase("{\"type\":\"response.completed\",\"response\":{\"usage\":{\"input_tokens\":\"many\"}}}")]
        public void FieldOfTheWrongJsonType_ThrowsProtocol(string data)
        {
            var reducer = new ResponsesStreamReducer(Model);

            var e = Assert.Throws<ProviderException>(() => reducer.Apply(new SseEvent(null, data), new List<ModelEvent>()));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        }

        [Test]
        public void DoneWithoutCompletion_ThrowsProtocol()
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();
            reducer.Apply(new SseEvent("response.created", "{\"type\":\"response.created\",\"response\":{\"id\":\"resp_1\"}}"), output);

            var e = Assert.Throws<ProviderException>(() => reducer.Apply(new SseEvent(null, SseEvent.DoneSentinel), output));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        }

        [Test]
        public void UnknownEvents_AreIgnored()
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();

            reducer.Apply(new SseEvent("response.created", "{\"type\":\"response.created\",\"response\":{\"id\":\"r\"}}"), output);
            reducer.Apply(new SseEvent("response.future_thing", "{\"type\":\"response.future_thing\"}"), output);

            Assert.That(output.Single().Kind, Is.EqualTo(ModelEventKind.Started));
        }

        [Test]
        public void OutputChars_CountsTextAndArgumentDeltas()
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();

            foreach (var sse in SseFixtures.Parse("responses_commentary_tool"))
                reducer.Apply(sse, output);

            Assert.That(reducer.OutputChars, Is.EqualTo("On my way to the sofa.".Length + "{\"place\":\"sofa\"}".Length));
            Assert.That(reducer.IsCompleted, Is.True);
        }

        [Test]
        public void WebSearch_ReportsTheSearchAroundItsItemAndNeverAsAToolCall()
        {
            // Arrange: live 2026-11-02, "What's the weather like today?" with web_search and a London location.
            const string searchId = "ws_call_01a19b84889874d7a17f272794205720";

            // Act
            var events = Reduce("responses_web_search");

            // Assert
            var kinds = events.Select(e => e.Kind).ToList();
            Assert.That(kinds, Has.No.Member(ModelEventKind.ToolCall));
            var started = kinds.IndexOf(ModelEventKind.WebSearchStarted);
            var completed = kinds.IndexOf(ModelEventKind.WebSearchCompleted);
            Assert.That(started, Is.GreaterThan(kinds.IndexOf(ModelEventKind.TextDone)), "the preamble is spoken first");
            Assert.That(completed, Is.GreaterThan(started));
            Assert.That(completed, Is.LessThan(kinds.LastIndexOf(ModelEventKind.TextDelta)), "the answer follows the search");
            Assert.That(events[started].Text, Is.EqualTo(searchId));
            Assert.That(events[completed].Text, Is.EqualTo(searchId));
        }

        [Test]
        public void WebSearch_KeepsTheCallAsAnOpaqueItemAndCountsItInTheUsage()
        {
            // Act
            var result = Reduce("responses_web_search").Last().Result;

            // Assert
            Assert.That(result.FinishReason, Is.EqualTo(FinishReason.Stop));
            Assert.That(result.OutputItems.Select(i => i.Kind), Is.EqualTo(new[]
            {
                ItemKind.Opaque, ItemKind.AssistantText, ItemKind.Opaque, ItemKind.Opaque, ItemKind.AssistantText,
            }));
            Assert.That(result.OutputItems[2].Json, Does.Contain("\"type\":\"web_search_call\"").And.Contain("\"completed\""));
            Assert.That(result.OutputItems[4].Text, Does.StartWith("It's cloudy"));
            Assert.That(result.Usage.WebSearches, Is.EqualTo(1));
            Assert.That(result.Usage.InputTokens, Is.EqualTo(4374));
        }

        [Test]
        public void NamespacedFunctionCall_KeepsTheDottedName()
        {
            // Arrange: the item as streamed live (2026-11-02) for a tool sent inside {"type":"namespace","name":"actions"}.
            var item = "{\"arguments\":\"{}\",\"call_id\":\"call_1\",\"id\":\"fc_1\",\"name\":\"actions.dance\"," +
                       "\"status\":\"completed\",\"type\":\"function_call\"}";

            // Act
            var call = ReduceItems(item).Single(e => e.Kind == ModelEventKind.ToolCall).ToolCall;

            // Assert
            Assert.That(call.Name, Is.EqualTo("actions.dance"));
        }

        [Test]
        public void FunctionCallWithANamespaceField_IsNamedNamespaceDotFunction()
        {
            // Arrange: the shape of the tool-search docs, with the namespace in its own field.
            var item = "{\"arguments\":\"{}\",\"call_id\":\"call_1\",\"name\":\"dance\",\"namespace\":\"actions\"," +
                       "\"type\":\"function_call\"}";

            // Act
            var events = ReduceItems(item);

            // Assert
            Assert.That(events.Single(e => e.Kind == ModelEventKind.ToolCall).ToolCall.Name, Is.EqualTo("actions.dance"));
            Assert.That(events.Last().Result.OutputItems.Single().ToolName, Is.EqualTo("actions.dance"));
        }

        [Test]
        public void ToolSearchItems_AreKeptAsOpaqueItems()
        {
            // Arrange: items of a deferred namespace load (live probe 2026-11-02).
            var call = "{\"arguments\":{\"paths\":[\"actions\"]},\"id\":\"ts_1\",\"type\":\"tool_search_call\"}";
            var output = "{\"id\":\"tso_1\",\"tools\":[],\"type\":\"tool_search_output\"}";

            // Act
            var events = ReduceItems(call, output);

            // Assert
            Assert.That(events.Select(e => e.Kind), Has.No.Member(ModelEventKind.ToolCall));
            Assert.That(events.Last().Result.OutputItems.Select(i => i.Kind), Is.EqualTo(new[] { ItemKind.Opaque, ItemKind.Opaque }));
        }

        /// <summary>response.created, one response.output_item.done per item, response.completed.</summary>
        static List<ModelEvent> ReduceItems(params string[] items)
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();
            reducer.Apply(new SseEvent("response.created", "{\"type\":\"response.created\",\"response\":{\"id\":\"resp_1\"}}"), output);
            foreach (var item in items)
                reducer.Apply(new SseEvent("response.output_item.done", "{\"type\":\"response.output_item.done\",\"item\":" + item + "}"), output);
            reducer.Apply(new SseEvent("response.completed", "{\"type\":\"response.completed\",\"response\":{\"id\":\"resp_1\"}}"), output);
            return output;
        }

        static List<ModelEvent> Reduce(string fixture)
        {
            var reducer = new ResponsesStreamReducer(Model);
            var output = new List<ModelEvent>();
            foreach (var sse in SseFixtures.Parse(fixture))
            {
                if (!reducer.IsCompleted)
                    reducer.Apply(sse, output);
            }
            Assert.That(reducer.IsCompleted, Is.True);
            return output;
        }

        static string Text(IEnumerable<ModelEvent> events) =>
            string.Concat(events.Where(e => e.Kind == ModelEventKind.TextDelta).Select(e => e.Text));
    }
}
