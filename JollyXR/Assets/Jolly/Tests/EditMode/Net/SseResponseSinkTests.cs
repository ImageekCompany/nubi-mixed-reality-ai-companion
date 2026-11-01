using System.Text;
using Jolly.Net.Http;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    public sealed class SseResponseSinkTests
    {
        [Test]
        public void Receive_SuccessStatus_ParsesEvents()
        {
            var sink = new SseResponseSink();

            sink.Receive(200, Bytes("data: a\n\ndata: b"));
            sink.Complete();

            Assert.That(sink.TryTake(out var first), Is.True);
            Assert.That(sink.TryTake(out var second), Is.True);
            Assert.That(sink.TryTake(out _), Is.False);
            Assert.That(first.Data, Is.EqualTo("a"));
            Assert.That(second.Data, Is.EqualTo("b"));
            Assert.That(sink.ErrorBody, Is.Empty);
        }

        [Test]
        public void Receive_ErrorStatus_CapturesTheBodyInsteadOfParsingIt()
        {
            var sink = new SseResponseSink();

            sink.Receive(429, Bytes("{\"error\":"));
            sink.Receive(429, Bytes("{\"message\":\"slow down\"}}"));
            sink.Complete();

            Assert.That(sink.TryTake(out _), Is.False);
            Assert.That(sink.ErrorBody, Is.EqualTo("{\"error\":{\"message\":\"slow down\"}}"));
        }

        [Test]
        public void Receive_ErrorBodyLongerThanTheCap_IsTruncated()
        {
            var sink = new SseResponseSink(maxErrorBytes: 8);

            sink.Receive(500, Bytes("0123456789abcdef"));

            Assert.That(sink.ErrorBody, Is.EqualTo("01234567"));
        }

        static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);
    }
}
