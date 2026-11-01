using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>A mic whose sessions yield no chunks and complete on Stop (like the real one), or abort on Dispose.</summary>
    public sealed class FakeAudioCapture : IAudioCapture
    {
        public bool IsAvailable { get; set; } = true;
        public List<Session> Sessions { get; } = new();
        public Session Last => Sessions[Sessions.Count - 1];

        public IAudioCaptureSession Start(int targetSampleRate)
        {
            var session = new Session(targetSampleRate);
            Sessions.Add(session);
            return session;
        }

        public sealed class Session : IAudioCaptureSession
        {
            readonly UniTaskCompletionSource _end = new();

            public Session(int sampleRate)
            {
                SampleRate = sampleRate;
                Chunks = UniTaskAsyncEnumerable.Create<PcmChunk>(async (_, ct) => await _end.Task.AttachExternalCancellation(ct));
            }

            public int SampleRate { get; }
            public bool Stopped { get; private set; }
            public bool Disposed { get; private set; }
            public IUniTaskAsyncEnumerable<PcmChunk> Chunks { get; }
            public float Level => 0f;

            public void Stop()
            {
                Stopped = true;
                _end.TrySetResult();
            }

            public void Dispose()
            {
                Disposed = true;
                _end.TrySetCanceled();
            }
        }
    }
}
