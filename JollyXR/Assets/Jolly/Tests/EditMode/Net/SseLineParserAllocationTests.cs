using System;
using System.Collections.Generic;
using System.IO;
using Jolly.Net.Http;
using Jolly.Net.Sse;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    /// <summary>
    /// The SSE parser runs on the main thread for every streamed byte of a turn (DoD §4: ≤ 4 KB GC per frame). Once its
    /// buffers have grown, a stream may allocate only the data string of each event it hands out.
    /// </summary>
    public sealed class SseLineParserAllocationTests
    {
        [TestCase("responses_summary")]
        [TestCase("responses_parallel_tools")]
        public void Feed_RecordedStreamAfterWarmUp_AllocatesOneStringPerEvent(string fixture)
        {
            var bytes = File.ReadAllBytes(SseFixtures.PathOf(fixture));
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>(256);
            FeedInChunks(parser, bytes, events);
            var count = events.Count;
            events.Clear();

            var allocations = GcAllocations.Count(() => FeedInChunks(parser, bytes, events));

            Assert.That(events.Count, Is.EqualTo(count));
            Assert.That(allocations, Is.LessThanOrEqualTo(count));
        }

        [Test]
        public void ResponseSink_ReusedAfterReset_AllocatesOneStringPerEvent()
        {
            var bytes = File.ReadAllBytes(SseFixtures.PathOf("responses_summary"));
            var sink = new SseResponseSink();
            var count = Drain(sink, bytes);
            sink.Reset();

            var allocations = GcAllocations.Count(() => Drain(sink, bytes));

            Assert.That(allocations, Is.LessThanOrEqualTo(count));
        }

        static int Drain(SseResponseSink sink, byte[] bytes)
        {
            sink.Receive(200, bytes);
            sink.Complete();
            var count = 0;
            while (sink.TryTake(out _))
                count++;
            return count;
        }

        /// <summary>The 32 KB receive buffer of SseDownloadHandler arrives in network-sized slices.</summary>
        static void FeedInChunks(SseLineParser parser, byte[] bytes, Queue<SseEvent> events)
        {
            const int chunk = 1400;
            for (var offset = 0; offset < bytes.Length; offset += chunk)
                parser.Feed(bytes.AsSpan(offset, Math.Min(chunk, bytes.Length - offset)), events);
            parser.Complete(events);
        }
    }
}
