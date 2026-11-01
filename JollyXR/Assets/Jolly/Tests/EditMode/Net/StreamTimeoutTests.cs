using System;
using System.Threading;
using Jolly.Net.Http;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    public sealed class StreamTimeoutTests
    {
        [Test]
        public void Restart_ThenSilenceForTheWholeWindow_CancelsTheToken()
        {
            var clock = new FakeClock();
            using var timeout = new StreamTimeout(clock, CancellationToken.None);

            timeout.Restart(TimeSpan.FromSeconds(6));
            clock.Advance(TimeSpan.FromSeconds(5.9));
            var early = timeout.Token.IsCancellationRequested;
            clock.Advance(TimeSpan.FromSeconds(0.2));

            Assert.That(early, Is.False);
            Assert.That(timeout.Token.IsCancellationRequested, Is.True);
            Assert.That(timeout.HasFired, Is.True);
        }

        [Test]
        public void Restart_BeforeTheDeadline_ExtendsTheWindow()
        {
            var clock = new FakeClock();
            using var timeout = new StreamTimeout(clock, CancellationToken.None);

            timeout.Restart(TimeSpan.FromSeconds(6));
            clock.Advance(TimeSpan.FromSeconds(5));
            timeout.Restart(TimeSpan.FromSeconds(10));
            clock.Advance(TimeSpan.FromSeconds(9));
            var extended = timeout.Token.IsCancellationRequested;
            clock.Advance(TimeSpan.FromSeconds(1.1));

            Assert.That(extended, Is.False);
            Assert.That(timeout.HasFired, Is.True);
        }

        [Test]
        public void ParentCancellation_CancelsTheTokenWithoutFiring()
        {
            var clock = new FakeClock();
            using var parent = new CancellationTokenSource();
            using var timeout = new StreamTimeout(clock, parent.Token);
            timeout.Restart(TimeSpan.FromSeconds(6));

            parent.Cancel();

            Assert.That(timeout.Token.IsCancellationRequested, Is.True);
            Assert.That(timeout.HasFired, Is.False);
        }

        [Test]
        public void Dispose_StopsTheTimerLoop()
        {
            var clock = new FakeClock();
            var timeout = new StreamTimeout(clock, CancellationToken.None);
            timeout.Restart(TimeSpan.FromSeconds(6));

            timeout.Dispose();

            Assert.That(clock.PendingDelays, Is.Zero);
            Assert.That(timeout.HasFired, Is.False);
        }
    }
}
