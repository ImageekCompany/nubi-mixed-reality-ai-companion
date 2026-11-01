using Jolly.Providers.MetaApi.Asr;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Asr
{
    public sealed class AudioSendPacerTests
    {
        [Test]
        public void Backlog_UpToTheLimit_IsSentWithoutWaiting()
        {
            var pacer = new AudioSendPacer(5.0);

            for (var i = 0; i < 62; i++)
            {
                Assert.That(pacer.DelayBeforeSending(0.08, now: 10), Is.Zero, $"chunk {i}");
                pacer.OnSent(0.08, now: 10);
            }

            Assert.That(pacer.SentSeconds, Is.EqualTo(4.96).Within(1e-9));
        }

        [Test]
        public void Chunk_ThatWouldGoPastTheLimit_WaitsForTheWallClock()
        {
            var pacer = new AudioSendPacer(5.0);
            pacer.OnSent(4.96, now: 10);

            var delay = pacer.DelayBeforeSending(0.08, now: 10);

            Assert.That(delay, Is.EqualTo(0.04).Within(1e-9));
        }

        [Test]
        public void LiveAudio_ArrivingInRealTime_NeverWaits()
        {
            var pacer = new AudioSendPacer(5.0);
            pacer.OnSent(3.0, now: 0);

            for (var i = 1; i <= 100; i++)
            {
                var now = i * 0.08;
                Assert.That(pacer.DelayBeforeSending(0.08, now), Is.Zero);
                pacer.OnSent(0.08, now);
            }
        }

        [Test]
        public void WallClock_StartsAtTheFirstSend()
        {
            var pacer = new AudioSendPacer(1.0);
            pacer.OnSent(1.0, now: 100);

            Assert.That(pacer.DelayBeforeSending(0.5, now: 100.25), Is.EqualTo(0.25).Within(1e-9));
        }
    }
}
