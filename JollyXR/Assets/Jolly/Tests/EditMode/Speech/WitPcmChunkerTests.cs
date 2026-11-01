using Jolly.Contracts;
using Jolly.Providers.Wit;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class WitPcmChunkerTests
    {
        static short[] Samples(PcmChunk chunk) => chunk.Span.ToArray();

        [Test]
        public void Feed_EvenBytes_DecodesLittleEndianSamples()
        {
            var chunker = new WitPcmChunker(24000);

            using var chunk = chunker.Feed(new byte[] { 0x01, 0x02, 0xFF, 0x7F, 0x00, 0x80 }, 6);

            Assert.That(Samples(chunk), Is.EqualTo(new short[] { 0x0201, short.MaxValue, short.MinValue }));
            Assert.That(chunk.SampleRate, Is.EqualTo(24000));
            Assert.That(chunk.CaptureTime, Is.Zero);
        }

        [Test]
        public void Feed_OddByte_IsCarriedIntoTheNextChunk()
        {
            var chunker = new WitPcmChunker(24000);

            using var first = chunker.Feed(new byte[] { 0x01, 0x02, 0x03 }, 3);
            using var second = chunker.Feed(new byte[] { 0x04, 0x05, 0x06, 0x07 }, 3);

            Assert.That(Samples(first), Is.EqualTo(new short[] { 0x0201 }));
            Assert.That(Samples(second), Is.EqualTo(new short[] { 0x0403, 0x0605 }));
        }

        [Test]
        public void Feed_SingleByte_ReturnsNullUntilTheSampleCompletes()
        {
            var chunker = new WitPcmChunker(24000);

            var none = chunker.Feed(new byte[] { 0x34 }, 1);
            using var chunk = chunker.Feed(new byte[] { 0x12 }, 1);

            Assert.That(none, Is.Null);
            Assert.That(Samples(chunk), Is.EqualTo(new short[] { 0x1234 }));
        }

        [Test]
        public void Feed_RespectsDataLengthNotArrayLength()
        {
            var chunker = new WitPcmChunker(24000);

            using var chunk = chunker.Feed(new byte[] { 0x01, 0x00, 0x09, 0x09 }, 2);

            Assert.That(Samples(chunk), Is.EqualTo(new short[] { 1 }));
        }

        [Test]
        public void PcmChunk_DoubleDispose_ReturnsInstanceToPoolOnlyOnce()
        {
            var chunk = PcmChunk.Rent(4, 24000);

            chunk.Dispose();
            Assert.DoesNotThrow(chunk.Dispose);
            var a = PcmChunk.Rent(4, 24000);
            var b = PcmChunk.Rent(4, 24000);

            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(a.Buffer, Is.Not.SameAs(b.Buffer));
            a.Dispose();
            b.Dispose();
        }
    }
}
