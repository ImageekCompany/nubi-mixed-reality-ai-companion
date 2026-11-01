using System.Collections.Generic;
using System.Linq;
using Jolly.Contracts;
using Jolly.Net.Sse;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Tests.EditMode.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Chat
{
    /// <summary>
    /// response.created and response.completed echo the whole request (instructions, tools, output). The reducer runs
    /// on the main thread during every turn (DoD §4: ≤ 4 KB GC per frame), so it must not build that echo's tree.
    /// </summary>
    public sealed class ResponsesStreamReducerAllocationTests
    {
        [TestCase("response.created")]
        [TestCase("response.completed")]
        public void EnvelopeEvent_AllocatesLessThanHalfAsOftenAsAFullParse(string eventName)
        {
            var sse = SseFixtures.Parse("responses_summary").First(e => e.EventName == eventName);
            JObject.Parse(sse.Data);
            Reduce(sse);

            var fullParse = GcAllocations.Count(() => JObject.Parse(sse.Data));
            var reduced = GcAllocations.Count(() => Reduce(sse));

            Assert.That(reduced, Is.LessThan(fullParse / 2));
        }

        [Test]
        public void CompletedEnvelope_StillYieldsIdAndUsage()
        {
            var events = SseFixtures.Parse("responses_summary");
            var reducer = new ResponsesStreamReducer("m");
            var output = new List<ModelEvent>();

            foreach (var sse in events)
                reducer.Apply(sse, output);

            var result = output.Last().Result;
            Assert.That(result.ResponseId, Is.EqualTo("resp_6ae6e3c775611c2560c34d0d"));
            Assert.That(result.Usage.InputTokens, Is.EqualTo(508));
            Assert.That(result.Usage.OutputTokens, Is.EqualTo(272));
        }

        static void Reduce(SseEvent sse) => new ResponsesStreamReducer("m").Apply(sse, new List<ModelEvent>(4));
    }
}
