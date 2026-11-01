using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing
{
    /// <summary>Output that plays nothing by itself; tests raise <see cref="FirstSamplePlayed"/> through the queue fake.</summary>
    public sealed class FakeSpeechOutput : ISpeechOutput
    {
        public bool IsPlaying { get; set; }
        public int Flushes { get; private set; }
        public event Action<int> FirstSamplePlayed;

        public UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct) => UniTask.CompletedTask;
        public void Flush() => Flushes++;
        public void RaiseFirstSample(int utteranceId) => FirstSamplePlayed?.Invoke(utteranceId);
    }
}
