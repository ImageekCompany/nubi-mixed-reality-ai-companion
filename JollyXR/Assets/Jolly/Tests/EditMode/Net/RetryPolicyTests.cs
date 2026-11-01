using System;
using Jolly.Contracts;
using Jolly.Net.Retry;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    public sealed class RetryPolicyTests
    {
        [TestCase(429, ProviderErrorKind.RateLimited)]
        [TestCase(500, ProviderErrorKind.Server)]
        [TestCase(502, ProviderErrorKind.Server)]
        [TestCase(503, ProviderErrorKind.Server)]
        [TestCase(0, ProviderErrorKind.Network)]
        [TestCase(0, ProviderErrorKind.Timeout)]
        public void NextDelay_TransientFailure_IsRetried(int status, ProviderErrorKind kind)
        {
            var policy = new RetryPolicy();

            var delay = policy.NextDelay(1, Error(kind, status), new FakeRandom());

            Assert.That(delay, Is.Not.Null);
        }

        [TestCase(400, ProviderErrorKind.BadRequest)]
        [TestCase(401, ProviderErrorKind.Unauthorized)]
        [TestCase(403, ProviderErrorKind.Unauthorized)]
        [TestCase(404, ProviderErrorKind.BadRequest)]
        [TestCase(413, ProviderErrorKind.BadRequest)]
        [TestCase(504, ProviderErrorKind.Server)]
        [TestCase(0, ProviderErrorKind.Protocol)]
        [TestCase(0, ProviderErrorKind.BudgetExceeded)]
        public void NextDelay_PermanentFailure_IsNeverRetried(int status, ProviderErrorKind kind)
        {
            var policy = new RetryPolicy();

            Assert.That(policy.NextDelay(1, Error(kind, status), new FakeRandom()), Is.Null);
        }

        [Test]
        public void NextDelay_AfterMaxAttempts_StopsRetrying()
        {
            var policy = new RetryPolicy(maxAttempts: 3);
            var e = Error(ProviderErrorKind.Server, 503);

            Assert.That(policy.NextDelay(2, e, new FakeRandom()), Is.Not.Null);
            Assert.That(policy.NextDelay(3, e, new FakeRandom()), Is.Null);
        }

        [TestCase(1, 0.5)]
        [TestCase(2, 1.0)]
        [TestCase(3, 2.0)]
        [TestCase(4, 4.0)]
        [TestCase(6, 4.0)]
        public void NextDelay_FullJitter_IsBoundedByTheExponentialCap(int attempt, double capSeconds)
        {
            var policy = new RetryPolicy(maxAttempts: 10);
            var e = Error(ProviderErrorKind.Server, 500);

            var top = policy.NextDelay(attempt, e, new FakeRandom().EnqueueFloat(float.MaxValue));
            var bottom = policy.NextDelay(attempt, e, new FakeRandom().EnqueueFloat(float.MinValue));

            Assert.That(top.Value.TotalSeconds, Is.EqualTo(capSeconds).Within(1e-3));
            Assert.That(bottom.Value, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void NextDelay_SeededRandom_IsDeterministicAndWithinTheCap()
        {
            var policy = new RetryPolicy();
            var e = Error(ProviderErrorKind.Network, 0);

            var a = policy.NextDelay(2, e, new FakeRandom(seed: 42));
            var b = policy.NextDelay(2, e, new FakeRandom(seed: 42));

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.Value.TotalSeconds, Is.InRange(0.0, 1.0));
        }

        [Test]
        public void NextDelay_RetryAfter_IsHonouredAndCappedAt8Seconds()
        {
            var policy = new RetryPolicy();

            var honoured = policy.NextDelay(1, Error(ProviderErrorKind.RateLimited, 429, TimeSpan.FromSeconds(3)), new FakeRandom());
            var capped = policy.NextDelay(1, Error(ProviderErrorKind.RateLimited, 429, TimeSpan.FromSeconds(30)), new FakeRandom());

            Assert.That(honoured, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(capped, Is.EqualTo(TimeSpan.FromSeconds(8)));
        }

        static ProviderException Error(ProviderErrorKind kind, int status, TimeSpan? retryAfter = null) =>
            new("test", kind, "boom", status, retryAfter: retryAfter);
    }
}
