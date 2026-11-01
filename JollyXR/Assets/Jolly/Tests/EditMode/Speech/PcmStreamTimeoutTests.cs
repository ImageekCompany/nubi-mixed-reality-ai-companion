using System;
using System.Threading;
using Jolly.Contracts;
using Jolly.Providers.Wit;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Speech
{
    public sealed class PcmStreamTimeoutTests
    {
        static readonly TimeSpan FirstByte = TimeSpan.FromSeconds(3);
        static readonly TimeSpan Idle = TimeSpan.FromSeconds(3);

        FakeClock _clock;
        ControlledPcmStream _source;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _source = new ControlledPcmStream();
        }

        StreamProbe<PcmChunk> Start(CancellationToken ct = default)
            => StreamProbe<PcmChunk>.Start(PcmStreamTimeout.Apply(_source, _clock, FirstByte, Idle, "wit"), ct);

        static void AssertTimeout(StreamProbe<PcmChunk> probe)
        {
            Assert.That(probe.Error, Is.InstanceOf<ProviderException>());
            var error = (ProviderException)probe.Error;
            Assert.That(error.Kind, Is.EqualTo(ProviderErrorKind.Timeout));
            Assert.That(error.Provider, Is.EqualTo("wit"));
        }

        [Test]
        public void NoFirstChunkWithinTimeout_ThrowsTimeout()
        {
            var probe = Start();

            _clock.Advance(TimeSpan.FromSeconds(2.9));
            var before = probe.IsRunning;
            _clock.Advance(TimeSpan.FromSeconds(0.2));

            Assert.That(before, Is.True);
            AssertTimeout(probe);
            Assert.That(_source.Token.IsCancellationRequested, Is.True);
        }

        [Test]
        public void GapBetweenChunksLongerThanIdle_ThrowsTimeout()
        {
            var probe = Start();
            _clock.Advance(TimeSpan.FromSeconds(1));
            _source.Emit(1, 2);

            _clock.Advance(TimeSpan.FromSeconds(2.9));
            var before = probe.IsRunning;
            _clock.Advance(TimeSpan.FromSeconds(0.2));

            Assert.That(before, Is.True);
            Assert.That(probe.Items, Has.Count.EqualTo(1));
            AssertTimeout(probe);
        }

        [Test]
        public void SteadyChunks_PassThroughAndComplete()
        {
            var probe = Start();

            for (var i = 0; i < 5; i++)
            {
                _clock.Advance(TimeSpan.FromSeconds(2.5));
                _source.Emit((short)i);
            }
            _source.Complete();

            Assert.That(probe.Completed, Is.True);
            Assert.That(probe.Items, Has.Count.EqualTo(5));
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void CallerCancellation_IsNotReportedAsTimeout()
        {
            using var cts = new CancellationTokenSource();
            var probe = Start(cts.Token);

            cts.Cancel();

            Assert.That(probe.Canceled, Is.True);
            Assert.That(probe.Error, Is.Null);
            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void SourceError_IsPropagatedUnchanged()
        {
            var probe = Start();
            var error = new ProviderException("wit", ProviderErrorKind.Unauthorized, "401", 401);

            _source.Fail(error);

            Assert.That(probe.Error, Is.SameAs(error));
        }
    }
}
