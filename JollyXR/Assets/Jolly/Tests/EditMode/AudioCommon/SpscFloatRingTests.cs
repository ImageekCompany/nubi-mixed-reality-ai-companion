using System;
using System.Threading;
using Jolly.Audio.Common;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.AudioCommon
{
    public sealed class SpscFloatRingTests
    {
        [Test]
        public void Read_ReturnsSamplesInWriteOrder()
        {
            var ring = new SpscFloatRing(8);
            var read = new float[3];

            ring.Write(new[] { 1f, 2f, 3f });
            var count = ring.Read(read);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(read, Is.EqualTo(new[] { 1f, 2f, 3f }));
            Assert.That(ring.Count, Is.Zero);
        }

        [Test]
        public void Write_WhenFull_WritesOnlyFreeSpace()
        {
            var ring = new SpscFloatRing(4);

            var first = ring.Write(new[] { 1f, 2f, 3f });
            var second = ring.Write(new[] { 4f, 5f, 6f });

            Assert.That(first, Is.EqualTo(3));
            Assert.That(second, Is.EqualTo(1));
            Assert.That(ring.Count, Is.EqualTo(4));
            Assert.That(ring.FreeSpace, Is.Zero);
        }

        [Test]
        public void ReadAndWrite_AcrossTheEndOfTheBuffer_WrapAround()
        {
            var ring = new SpscFloatRing(5);
            ring.Write(new[] { 0f, 0f, 0f, 0f });
            ring.Read(new float[4]);
            var read = new float[4];

            var written = ring.Write(new[] { 1f, 2f, 3f, 4f });
            var count = ring.Read(read);

            Assert.That(written, Is.EqualTo(4));
            Assert.That(count, Is.EqualTo(4));
            Assert.That(read, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
        }

        [Test]
        public void Read_OnUnderrun_ReturnsAvailableCountAndLeavesTheRestUntouched()
        {
            var ring = new SpscFloatRing(8);
            ring.Write(new[] { 1f, 2f });
            var read = new[] { 9f, 9f, 9f, 9f };

            var count = ring.Read(read);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(read, Is.EqualTo(new[] { 1f, 2f, 9f, 9f }));
        }

        [Test]
        public void Read_WhenEmpty_ReturnsZero()
        {
            var ring = new SpscFloatRing(8);

            Assert.That(ring.Read(new float[4]), Is.Zero);
        }

        [Test]
        public void Clear_DropsQueuedSamples()
        {
            var ring = new SpscFloatRing(8);
            ring.Write(new[] { 1f, 2f, 3f });

            ring.Clear();

            Assert.That(ring.Count, Is.Zero);
            Assert.That(ring.FreeSpace, Is.EqualTo(8));
            Assert.That(ring.Read(new float[3]), Is.Zero);
        }

        [Test]
        public void Constructor_RejectsNonPositiveCapacity()
        {
            Assert.That(() => new SpscFloatRing(0), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ConcurrentProducerAndConsumer_PreserveOrderWithoutLoss()
        {
            const int total = 200_000;
            var ring = new SpscFloatRing(1024);
            var producer = new Thread(() =>
            {
                var chunk = new float[97];
                var next = 0;
                while (next < total)
                {
                    var n = Math.Min(chunk.Length, total - next);
                    for (var i = 0; i < n; i++)
                        chunk[i] = next + i;
                    var written = ring.Write(new ReadOnlySpan<float>(chunk, 0, n));
                    next += written;
                    if (written == 0)
                        Thread.Yield();
                }
            });

            producer.Start();
            var buffer = new float[61];
            var expected = 0;
            var mismatch = -1;
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (expected < total && DateTime.UtcNow < deadline)
            {
                var n = ring.Read(buffer);
                for (var i = 0; i < n; i++, expected++)
                {
                    if (mismatch < 0 && buffer[i] != expected)
                        mismatch = expected;
                }
            }
            producer.Join();

            Assert.That(mismatch, Is.EqualTo(-1), "first out-of-order sample index");
            Assert.That(expected, Is.EqualTo(total));
        }
    }
}
