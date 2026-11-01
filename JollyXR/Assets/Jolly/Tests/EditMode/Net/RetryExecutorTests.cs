using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Retry;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Net
{
    public sealed class RetryExecutorTests
    {
        FakeClock _clock;
        RecordingLog _log;
        RetryExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _log = new RecordingLog();
            _executor = new RetryExecutor(_clock, new FakeRandom().EnqueueFloat(1f, 1f), _log);
        }

        [Test]
        public void Execute_TransientFailureThenSuccess_WaitsTheJitteredDelayAndReturns()
        {
            var calls = 0;
            var task = _executor.Execute(_ =>
            {
                calls++;
                return calls == 1 ? Fail(ProviderErrorKind.Server, 503) : UniTask.FromResult(42);
            }, new RetryPolicy(), CancellationToken.None);

            var pendingDuringBackoff = task.Status;
            _clock.Advance(TimeSpan.FromSeconds(0.5));

            Assert.That(pendingDuringBackoff, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(task.GetAwaiter().GetResult(), Is.EqualTo(42));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=net.retry"), Is.True);
        }

        [Test]
        public void Execute_PermanentFailure_ThrowsWithoutRetrying()
        {
            var calls = 0;
            var task = _executor.Execute(_ =>
            {
                calls++;
                return Fail(ProviderErrorKind.Unauthorized, 401);
            }, new RetryPolicy(), CancellationToken.None);

            var e = Assert.Throws<ProviderException>(() => task.GetAwaiter().GetResult());
            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Unauthorized));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Execute_AttemptsExhausted_RethrowsTheLastFailure()
        {
            var calls = 0;
            var task = _executor.Execute(_ =>
            {
                calls++;
                return Fail(ProviderErrorKind.Timeout, 0);
            }, new RetryPolicy(maxAttempts: 2), CancellationToken.None);
            _clock.Advance(TimeSpan.FromSeconds(10));

            var e = Assert.Throws<ProviderException>(() => task.GetAwaiter().GetResult());
            Assert.That(e.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void Execute_CancelledDuringBackoff_ThrowsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();
            var task = _executor.Execute(_ => Fail(ProviderErrorKind.Server, 500), new RetryPolicy(), cts.Token);

            cts.Cancel();

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Canceled));
        }

        static UniTask<int> Fail(ProviderErrorKind kind, int status) =>
            UniTask.FromException<int>(new ProviderException("test", kind, "boom", status));
    }
}
