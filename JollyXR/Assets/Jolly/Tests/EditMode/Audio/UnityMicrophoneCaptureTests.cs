using System;
using Jolly.Audio.Capture;
using Jolly.Contracts;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Audio
{
    public sealed class UnityMicrophoneCaptureTests
    {
        FakeMicrophoneDevice _device;
        UnityMicrophoneCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _device = new FakeMicrophoneDevice(24000, 24000);
            _capture = new UnityMicrophoneCapture(_device, new FakeClock(), new RecordingLog());
        }

        [TearDown]
        public void TearDown() => _capture.Dispose();

        [Test]
        public void Start_WhileASessionIsCapturing_AbortsTheFirstSession()
        {
            var first = (MicrophoneCaptureSession)_capture.Start(24000);
            var firstChunks = new StreamCollector<PcmChunk>(first.Chunks);

            var second = (MicrophoneCaptureSession)_capture.Start(24000);

            Assert.That(firstChunks.Error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(first.IsCapturing, Is.False);
            Assert.That(second.IsCapturing, Is.True);
            Assert.That(_device.Starts, Is.EqualTo(2));
            Assert.That(_device.IsRecording, Is.True);
        }

        [Test]
        public void Dispose_AbortsTheActiveSession()
        {
            var session = (MicrophoneCaptureSession)_capture.Start(24000);
            var chunks = new StreamCollector<PcmChunk>(session.Chunks);

            _capture.Dispose();

            Assert.That(chunks.Error, Is.InstanceOf<OperationCanceledException>());
            Assert.That(_device.IsRecording, Is.False);
        }

        [Test]
        public void Start_AfterDispose_Throws()
        {
            _capture.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _capture.Start(24000));
        }
    }
}
