using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Testing
{
    public sealed class FakeClockTests
    {
        static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        [Test]
        public void Advance_MovesMonotonicAndWallTimeTogether()
        {
            var clock = new FakeClock(Start);

            clock.Advance(TimeSpan.FromSeconds(1.5));

            Assert.That(clock.Now, Is.EqualTo(1.5));
            Assert.That(clock.UtcNow, Is.EqualTo(Start.AddSeconds(1.5)));
        }

        [Test]
        public void Delay_CompletesOnlyWhenTimeReachesItsDueTime()
        {
            var clock = new FakeClock(Start);
            var delay = clock.Delay(TimeSpan.FromSeconds(2), CancellationToken.None);

            clock.Advance(TimeSpan.FromSeconds(1.999));
            var before = delay.Status;
            clock.Advance(TimeSpan.FromMilliseconds(1));

            Assert.That(before, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(delay.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void Delay_OfZero_CompletesSynchronously()
        {
            var clock = new FakeClock(Start);

            var delay = clock.Delay(TimeSpan.Zero, CancellationToken.None);

            Assert.That(delay.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Delay_WhenCancelled_FaultsWithCancellationAndIsForgotten()
        {
            var clock = new FakeClock(Start);
            using var cts = new CancellationTokenSource();
            var delay = clock.Delay(TimeSpan.FromSeconds(5), cts.Token);

            cts.Cancel();

            Assert.That(delay.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.That(clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void Advance_CompletesDelaysInDueOrder()
        {
            var clock = new FakeClock(Start);
            var order = "";
            Track(clock.Delay(TimeSpan.FromSeconds(3), CancellationToken.None), () => order += "3");
            Track(clock.Delay(TimeSpan.FromSeconds(1), CancellationToken.None), () => order += "1");
            Track(clock.Delay(TimeSpan.FromSeconds(2), CancellationToken.None), () => order += "2");

            clock.Advance(TimeSpan.FromSeconds(3));

            Assert.That(order, Is.EqualTo("123"));
        }

        static void Track(UniTask delay, Action onCompleted) =>
            delay.GetAwaiter().OnCompleted(onCompleted);
    }
}
