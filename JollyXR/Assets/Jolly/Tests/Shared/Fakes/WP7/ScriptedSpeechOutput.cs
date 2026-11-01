using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>
    /// ISpeechOutput whose playback the test drives: <see cref="StartPlaying"/> raises FirstSamplePlayed,
    /// <see cref="Finish"/> and <see cref="Fail"/> end a Play, <see cref="Flush"/> cancels every pending one.
    /// The PCM streams are recorded, not consumed.
    /// </summary>
    public sealed class ScriptedSpeechOutput : ISpeechOutput
    {
        public sealed class Playback
        {
            public int UtteranceId;
            public IUniTaskAsyncEnumerable<PcmChunk> Pcm;
            public AudioClip Clip;
            public CancellationToken Token;
            public readonly UniTaskCompletionSource Completion = new();
        }

        readonly List<Playback> _playbacks = new();

        public IReadOnlyList<Playback> Playbacks => _playbacks;
        public int FlushCount { get; private set; }
        public bool IsPlaying => PendingCount > 0;

        public int PendingCount
        {
            get
            {
                var count = 0;
                foreach (var p in _playbacks)
                {
                    if (p.Completion.Task.Status == UniTaskStatus.Pending)
                        count++;
                }
                return count;
            }
        }

        public event Action<int> FirstSamplePlayed;

        public UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct)
            => Track(new Playback { UtteranceId = utteranceId, Pcm = pcm, Token = ct });

        public UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct)
            => Track(new Playback { UtteranceId = utteranceId, Clip = clip, Token = ct });

        public void Flush()
        {
            FlushCount++;
            foreach (var p in _playbacks)
                p.Completion.TrySetCanceled();
        }

        public void StartPlaying(int utteranceId) => FirstSamplePlayed?.Invoke(utteranceId);

        public void Finish(int utteranceId) => Find(utteranceId).Completion.TrySetResult();

        public void Fail(int utteranceId, Exception error) => Find(utteranceId).Completion.TrySetException(error);

        Playback Find(int utteranceId)
        {
            foreach (var p in _playbacks)
            {
                if (p.UtteranceId == utteranceId)
                    return p;
            }
            throw new InvalidOperationException($"No playback for utterance {utteranceId}.");
        }

        UniTask Track(Playback playback)
        {
            _playbacks.Add(playback);
            if (playback.Token.CanBeCanceled)
                playback.Token.Register(() => playback.Completion.TrySetCanceled());
            return playback.Completion.Task;
        }
    }
}
