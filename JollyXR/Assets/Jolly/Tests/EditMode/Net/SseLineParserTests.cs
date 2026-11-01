using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jolly.Net.Sse;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    public sealed class SseLineParserTests
    {
        [Test]
        public void Feed_NamedEvent_DispatchesOnBlankLine()
        {
            var events = Parse("event: response.created\ndata: {\"a\":1}\n\n");

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].EventName, Is.EqualTo("response.created"));
            Assert.That(events[0].Data, Is.EqualTo("{\"a\":1}"));
        }

        [Test]
        public void Feed_WithoutBlankLine_DoesNotDispatchUntilItArrives()
        {
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>();

            parser.Feed(Bytes("data: x\n"), events);
            var before = events.Count;
            parser.Feed(Bytes("\n"), events);

            Assert.That(before, Is.Zero);
            Assert.That(events.Single().Data, Is.EqualTo("x"));
        }

        [Test]
        public void Feed_CrLfLineEndings_AreTreatedLikeLf()
        {
            var events = Parse("event: e\r\ndata: one\r\n\r\ndata: two\r\n\r\n");

            Assert.That(events.Select(e => e.Data), Is.EqualTo(new[] { "one", "two" }));
            Assert.That(events[0].EventName, Is.EqualTo("e"));
            Assert.That(events[1].EventName, Is.Null);
        }

        [Test]
        public void Feed_CrLfSplitAcrossChunks_DoesNotProduceAnExtraBlankLine()
        {
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>();

            parser.Feed(Bytes("data: a\r"), events);
            parser.Feed(Bytes("\ndata: b\r\n\r\n"), events);

            Assert.That(events.Single().Data, Is.EqualTo("a\nb"));
        }

        [Test]
        public void Feed_BareCr_EndsALine()
        {
            var events = Parse("data: a\r\rdata: b\r\r");

            Assert.That(events.Select(e => e.Data), Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Feed_MultiLineData_IsJoinedWithNewlines()
        {
            var events = Parse("data: first\ndata:second\ndata:  third\n\n");

            Assert.That(events.Single().Data, Is.EqualTo("first\nsecond\n third"));
        }

        [Test]
        public void Feed_DoneSentinel_IsAnOrdinaryDataEventFlaggedAsDone()
        {
            var events = Parse("data: [DONE]\n\n");

            Assert.That(events.Single().IsDone, Is.True);
            Assert.That(events.Single().EventName, Is.Null);
        }

        [Test]
        public void Feed_CommentsAndUnknownFields_AreIgnored()
        {
            var events = Parse(": keep-alive\nid: 7\nretry: 100\nfoo: bar\ndata: x\n\n");

            Assert.That(events.Single().Data, Is.EqualTo("x"));
        }

        [Test]
        public void Feed_EventWithoutData_IsNotDispatchedAndItsNameDoesNotLeak()
        {
            var events = Parse("event: ping\n\ndata: x\n\n");

            Assert.That(events.Single().EventName, Is.Null);
        }

        [Test]
        public void Feed_MultiByteCharacterSplitAcrossChunks_DecodesCorrectly()
        {
            var bytes = Bytes("data: café — 😀\n\n");
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>();

            for (var i = 0; i < bytes.Length; i++)
                parser.Feed(bytes.AsSpan(i, 1), events);

            Assert.That(events.Single().Data, Is.EqualTo("café — 😀"));
        }

        [Test]
        public void Feed_SurrogatePairStraddlingTheDecodeSliceBeforeAFullSlice_DecodesCorrectly()
        {
            // 3 of the emoji's 4 bytes end the first 4096-byte slice; the next slice decodes to 4097 chars.
            var data = new string('a', 4096 - "data: ".Length - 3) + "😀" + new string('b', 4095);

            var events = Parse("data: " + data + "\n\n");

            Assert.That(events.Single().Data, Is.EqualTo(data));
        }

        [Test]
        public void Complete_FlushesAPendingEventWhenTheStreamEndsWithoutABlankLine()
        {
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>();

            parser.Feed(Bytes("data: tail"), events);
            parser.Complete(events);

            Assert.That(events.Single().Data, Is.EqualTo("tail"));
        }

        [Test]
        public void Feed_RecordedFixtureInAnyChunkSize_YieldsTheSameEvents()
        {
            var whole = SseFixtures.Parse("responses_commentary_tool", chunkSize: int.MaxValue);

            foreach (var size in new[] { 1, 3, 64, 4096 })
            {
                var chunked = SseFixtures.Parse("responses_commentary_tool", size);
                Assert.That(chunked.Select(e => e.EventName + "|" + e.Data), Is.EqualTo(whole.Select(e => e.EventName + "|" + e.Data)),
                    $"chunk size {size}");
            }
            Assert.That(whole.Last().IsDone, Is.True);
            Assert.That(whole.First().EventName, Is.EqualTo("response.created"));
        }

        static List<SseEvent> Parse(string text)
        {
            var parser = new SseLineParser();
            var events = new Queue<SseEvent>();
            parser.Feed(Bytes(text), events);
            parser.Complete(events);
            return events.ToList();
        }

        static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
    }
}
