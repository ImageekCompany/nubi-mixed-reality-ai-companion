using System.Linq;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Asr;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class MuseAsrEventParserTests
    {
        [Test]
        public void RecordedPushToTalkSession_ParsesAckPartialsAndOneFinal()
        {
            var events = AsrFixtures.Lines("realtime_ptt_24k.txt").Select(MuseAsrEventParser.Parse).ToArray();

            Assert.That(events[0].Kind, Is.EqualTo(MuseAsrEventKind.Ack));
            Assert.That(events[0].SessionId, Is.EqualTo("jolly-rt-3fd4e781"));
            Assert.That(events.Skip(1).Select(e => e.Kind), Is.All.EqualTo(MuseAsrEventKind.Transcript));
            Assert.That(events.Count(e => e.Final), Is.EqualTo(1));
            Assert.That(events[1].Text, Is.EqualTo("hey"));
            Assert.That(events[1].AudioMs, Is.EqualTo(880));
            Assert.That(events.Last().Final, Is.True);
            Assert.That(events.Last().Text, Is.EqualTo("hey Jolly walk to the kitchen and tell me what this pen is."));
            Assert.That(events.Last().AudioMs, Is.EqualTo(3760));
        }

        [Test]
        public void EndpointingEvents_AreReportedAsOther()
        {
            var events = AsrFixtures.Lines("realtime_endpointing_24k.txt").Select(MuseAsrEventParser.Parse).ToArray();

            Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[]
            {
                MuseAsrEventKind.Other, MuseAsrEventKind.Transcript, MuseAsrEventKind.Transcript,
                MuseAsrEventKind.Other, MuseAsrEventKind.Other, MuseAsrEventKind.Transcript,
            }));
            Assert.That(events.Last().Final, Is.True);
            Assert.That(events.Last().Text, Is.Empty);
        }

        [Test]
        public void ErrorFrames_CarryMessageTypeCodeAndParam()
        {
            var errors = AsrFixtures.Lines("realtime_errors.txt").Select(MuseAsrEventParser.Parse).ToArray();

            Assert.That(errors.Select(e => e.Kind), Is.All.EqualTo(MuseAsrEventKind.Error));
            Assert.That(errors[0].ErrorType, Is.EqualTo("authentication_error"));
            Assert.That(errors[0].ErrorCode, Is.EqualTo("invalid_api_key"));
            Assert.That(errors[0].SessionId, Is.EqualTo("voyager/duplex/0"));
            Assert.That(errors[1].ErrorParam, Is.EqualTo("audioEncoding"));
            Assert.That(errors[2].Message, Is.EqualTo("Malformed handshake request"));
        }

        [Test]
        public void UnknownTypesAndNestedValues_AreSkipped()
        {
            var e = MuseAsrEventParser.Parse("{\"type\":\"speaker\",\"label\":{\"id\":[1,2]},\"extra\":[{\"a\":1}]}");

            Assert.That(e.Kind, Is.EqualTo(MuseAsrEventKind.Other));
        }

        [Test]
        public void TranscriptFields_MayArriveInAnyOrder()
        {
            var e = MuseAsrEventParser.Parse("{\"final\":true,\"audioProcessedMs\":10,\"transcript\":\"hi\",\"type\":\"transcript\"}");

            Assert.That(e.Kind, Is.EqualTo(MuseAsrEventKind.Transcript));
            Assert.That(e.Final, Is.True);
            Assert.That(e.Text, Is.EqualTo("hi"));
        }

        [TestCase("not json")]
        [TestCase("[1,2]")]
        [TestCase("{\"type\":\"transcript\",")]
        public void MalformedFrames_ThrowProtocol(string frame)
        {
            var e = Assert.Throws<ProviderException>(() => MuseAsrEventParser.Parse(frame));

            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Protocol));
        }
    }
}
