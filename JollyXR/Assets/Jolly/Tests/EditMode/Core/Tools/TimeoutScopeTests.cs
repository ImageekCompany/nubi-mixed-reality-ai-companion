using System;
using System.Threading;
using Jolly.Core.Tools;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Core.Tools
{
    public sealed class TimeoutScopeTests
    {
        [Test]
        public void Token_CancelsWhenTheClockReachesTheTimeout()
        {
            var clock = new FakeClock();
            using var scope = new TimeoutScope(clock, TimeSpan.FromSeconds(5), CancellationToken.None);

            clock.Advance(TimeSpan.FromSeconds(4.9));
            var early = scope.Token.IsCancellationRequested;
            clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(early, Is.False);
            Assert.That(scope.Token.IsCancellationRequested && scope.TimedOut, Is.True);
        }

        [Test]
        public void ParentCancel_IsNotATimeout()
        {
            using var parent = new CancellationTokenSource();
            using var scope = new TimeoutScope(new FakeClock(), TimeSpan.FromSeconds(5), parent.Token);

            parent.Cancel();

            Assert.That(scope.Token.IsCancellationRequested, Is.True);
            Assert.That(scope.TimedOut, Is.False);
        }

        [Test]
        public void Dispose_ForgetsThePendingDelay()
        {
            var clock = new FakeClock();
            var scope = new TimeoutScope(clock, TimeSpan.FromSeconds(5), CancellationToken.None);

            scope.Dispose();
            clock.Advance(TimeSpan.FromSeconds(10));

            Assert.That(clock.PendingDelays, Is.Zero);
            Assert.That(scope.TimedOut, Is.False);
        }
    }
}
