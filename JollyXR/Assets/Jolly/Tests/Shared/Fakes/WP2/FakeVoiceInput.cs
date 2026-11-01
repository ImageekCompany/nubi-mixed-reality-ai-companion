using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    public sealed class FakeVoiceActivation : IVoiceActivation
    {
        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        public void Press()
        {
            IsHeld = true;
            Pressed?.Invoke();
        }

        public void Release()
        {
            IsHeld = false;
            Released?.Invoke();
        }
    }

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

    /// <summary>Yields <see cref="Partials"/> at once, then the <see cref="Final"/> when the audio completes (PTT release).</summary>
    public sealed class ScriptedSpeechToText : ISpeechToText
    {
        public List<string> Partials { get; } = new();
        public string Final { get; set; } = "Hi newbie";
        public ProviderException Failure { get; set; }
        /// <summary>Never sends the final (the Listening watchdog case).</summary>
        public bool Hang { get; set; }
        public SttOptions LastOptions { get; private set; }

        public IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options,
                                                                   CancellationToken ct)
        {
            LastOptions = options;
            return UniTaskAsyncEnumerable.Create<TranscriptEvent>(async (writer, enumeratorCt) =>
            {
                foreach (var partial in Partials)
                    await writer.YieldAsync(new TranscriptEvent(TranscriptKind.Partial, partial, 0));
                await foreach (var chunk in audio.WithCancellation(enumeratorCt))
                    chunk.Dispose();
                if (Failure != null)
                    throw Failure;
                if (Hang)
                    await UniTask.Never(enumeratorCt);
                await writer.YieldAsync(new TranscriptEvent(TranscriptKind.Final, Final, 0));
            });
        }
    }
}
