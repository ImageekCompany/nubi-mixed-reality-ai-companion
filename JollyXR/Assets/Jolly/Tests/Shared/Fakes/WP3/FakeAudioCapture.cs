using System;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>IAudioCapture whose sessions are fed by the test with <see cref="FakeAudioCaptureSession.Push"/>.</summary>
    public sealed class FakeAudioCapture : IAudioCapture
    {
        public bool IsAvailable { get; set; } = true;
        public int Starts { get; private set; }
        public FakeAudioCaptureSession Current { get; private set; }

        public IAudioCaptureSession Start(int targetSampleRate)
        {
            Starts++;
            Current = new FakeAudioCaptureSession(targetSampleRate);
            return Current;
        }
    }

    public sealed class FakeAudioCaptureSession : IAudioCaptureSession
    {
        readonly Channel<PcmChunk> _channel = Channel.CreateSingleConsumerUnbounded<PcmChunk>();

        public FakeAudioCaptureSession(int sampleRate)
        {
            SampleRate = sampleRate;
            Chunks = _channel.Reader.ReadAllAsync();
        }

        public int SampleRate { get; }
        public IUniTaskAsyncEnumerable<PcmChunk> Chunks { get; }
        public float Level { get; set; }
        public bool IsStopped { get; private set; }
        public bool IsDisposed { get; private set; }

        /// <summary>Emits one chunk of silence of the given duration.</summary>
        public void Push(TimeSpan duration) => _channel.Writer.TryWrite(PcmChunk.Rent((int)(SampleRate * duration.TotalSeconds), SampleRate));

        public void Stop()
        {
            IsStopped = true;
            _channel.Writer.TryComplete();
        }

        public void Dispose()
        {
            IsDisposed = true;
            _channel.Writer.TryComplete(new OperationCanceledException("The capture session was disposed."));
        }
    }
}
