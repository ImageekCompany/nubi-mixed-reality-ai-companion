using System;
using Jolly.Core.Connectivity;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Offline
{
    public sealed class ProbeBackoffTests
    {
        [Test]
        public void Next_DoublesFromMinimumAndCapsAtMaximum()
        {
            var backoff = new ProbeBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

            var delays = new double[6];
            for (var i = 0; i < delays.Length; i++)
                delays[i] = backoff.Next().TotalSeconds;

            Assert.That(delays, Is.EqualTo(new double[] { 5, 10, 20, 30, 30, 30 }));
        }

        [Test]
        public void Next_AfterManyAttempts_StaysAtMaximum()
        {
            var backoff = new ProbeBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

            for (var i = 0; i < 1000; i++)
                backoff.Next();

            Assert.That(backoff.Next(), Is.EqualTo(TimeSpan.FromSeconds(30)));
        }

        [Test]
        public void Reset_StartsAgainFromMinimum()
        {
            var backoff = new ProbeBackoff(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
            backoff.Next();
            backoff.Next();

            backoff.Reset();

            Assert.That(backoff.Next(), Is.EqualTo(TimeSpan.FromSeconds(5)));
        }
    }
}
