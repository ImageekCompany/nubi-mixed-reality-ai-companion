using System;
using System.Linq;
using Jolly.Audio.Capture;
using Jolly.Contracts;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Audio
{
    public sealed class MicrophoneCaptureSessionTests
    {
        FakeClock _clock;
        RecordingLog _log;

        [SetUp]
        public void SetUp()
        {
            _clock = new FakeClock();
            _log = new RecordingLog();
        }

        [Test]
        public void Pump_Resamples48KTo80MsChunksAt24K()
        {
            var device = new FakeMicrophoneDevice(48000, 96000);
            using var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);

            device.Produce(new float[9600]);
            session.Pump();

            Assert.That(chunks.Items.Count, Is.EqualTo(2));
            Assert.That(chunks.Items.Select(c => c.Length), Is.All.EqualTo(1920));
            Assert.That(chunks.Items.Select(c => c.SampleRate), Is.All.EqualTo(24000));
        }

        [Test]
        public void Pump_ReadsAcrossTheEndOfTheLoopingRingWithoutLoss()
        {
            var device = new FakeMicrophoneDevice(24000, 3000);
            using var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);

            device.Produce(FakeMicrophoneDevice.Ramp(0, 2500));
            session.Pump();
            device.Produce(FakeMicrophoneDevice.Ramp(2500, 2500));
            session.Pump();

            var samples = chunks.Items.SelectMany(c => c.Span.ToArray()).ToArray();
            Assert.That(samples, Is.EqualTo(Enumerable.Range(0, 3840).Select(i => (short)i)));
        }

        [Test]
        public void Stop_FlushesThePartialChunkCompletesTheStreamAndStopsTheDevice()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            using var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);
            device.Produce(FakeMicrophoneDevice.Ramp(0, 500));
            session.Pump();
            device.Produce(FakeMicrophoneDevice.Ramp(500, 500));

            session.Stop();

            Assert.That(chunks.Items.Count, Is.EqualTo(1));
            Assert.That(chunks.Items[0].Length, Is.EqualTo(1000));
            Assert.That(chunks.Succeeded, Is.True);
            Assert.That(device.IsRecording, Is.False);
            Assert.That(session.IsCapturing, Is.False);
        }

        [Test]
        public void Dispose_AbortsTheStreamWithCancellationAndStopsTheDevice()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);
            device.Produce(new float[500]);

            session.Dispose();
            session.Pump();

            Assert.That(chunks.Items, Is.Empty);
            Assert.That(chunks.Error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(device.IsRecording, Is.False);
        }

        [Test]
        public void Level_IsTheRmsOfTheLastChunk()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            using var session = Start(device, 24000);
            var square = Enumerable.Range(0, 1920).Select(i => i % 2 == 0 ? 0.5f : -0.5f).ToArray();

            device.Produce(square);
            session.Pump();

            Assert.That(session.Level, Is.EqualTo(0.5f).Within(1e-3f));
        }

        [Test]
        public void CaptureTime_IsTheClockTimeOfTheFirstSampleOfEachChunk()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            using var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);
            _clock.Advance(TimeSpan.FromSeconds(1));

            device.Produce(new float[4800]);
            session.Pump();

            Assert.That(chunks.Items[0].CaptureTime, Is.EqualTo(0.8).Within(1e-9));
            Assert.That(chunks.Items[1].CaptureTime, Is.EqualTo(0.88).Within(1e-9));
        }

        [Test]
        public void Watchdog_RestartsTheDeviceWhenTheHeadStallsFor500Ms()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            using var session = Start(device, 24000);

            _clock.Advance(TimeSpan.FromMilliseconds(499));
            session.Pump();
            var startsBefore = device.Starts;
            _clock.Advance(TimeSpan.FromMilliseconds(1));
            session.Pump();

            Assert.That(startsBefore, Is.EqualTo(1));
            Assert.That(device.Starts, Is.EqualTo(2));
            Assert.That(_log.Contains(LogLevel.Warning, "evt=mic.stall"), Is.True);
        }

        [Test]
        public void Watchdog_DoesNotFireWhileTheHeadMoves()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            using var session = Start(device, 24000);

            for (var i = 0; i < 10; i++)
            {
                _clock.Advance(TimeSpan.FromMilliseconds(100));
                device.Produce(new float[2400]);
                session.Pump();
            }

            Assert.That(device.Starts, Is.EqualTo(1));
        }

        [Test]
        public void Watchdog_AfterThreeFruitlessRestarts_EndsTheStreamNormally()
        {
            var device = new FakeMicrophoneDevice(24000, 24000);
            using var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);

            for (var i = 0; i < 4; i++)
            {
                _clock.Advance(TimeSpan.FromMilliseconds(500));
                session.Pump();
            }

            Assert.That(device.Starts, Is.EqualTo(4));
            Assert.That(chunks.Succeeded, Is.True);
            Assert.That(session.IsCapturing, Is.False);
            Assert.That(device.IsRecording, Is.False);
            Assert.That(_log.Contains(LogLevel.Error, "evt=mic.dead"), Is.True);
        }

        [Test]
        public void Start_WhenTheDeviceRefuses_FaultsTheStream()
        {
            var device = new FakeMicrophoneDevice(24000, 24000) { RefuseStart = true };
            using var session = Start(device, 24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);

            Assert.That(chunks.Error, Is.InstanceOf<InvalidOperationException>());
            Assert.That(session.IsCapturing, Is.False);
        }

        MicrophoneCaptureSession Start(FakeMicrophoneDevice device, int targetRate) =>
            new(device, targetRate, _clock, _log);
    }
}
