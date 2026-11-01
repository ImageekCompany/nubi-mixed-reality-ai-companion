using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>
    /// The speaker at its edge: plays each stream or clip in real time on the PlayerLoop (no audio device in batchmode),
    /// raises FirstSamplePlayed one frame after the first chunk, and Flush cancels every pending play at once.
    /// </summary>
    public sealed class SimulatedSpeechOutput : ISpeechOutput
    {
        CancellationTokenSource _flush = new();
        int _active;

        public bool IsPlaying => _active > 0;
        public int FlushCount { get; private set; }
        public event Action<int> FirstSamplePlayed;

        public async UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _flush.Token);
            _active++;
            try
            {
                double seconds = 0;
                var first = true;
                await foreach (var chunk in pcm.WithCancellation(linked.Token))
                {
                    using (chunk)
                        seconds += (double)chunk.Length / chunk.SampleRate;
                    if (!first)
                        continue;
                    first = false;
                    await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
                    FirstSamplePlayed?.Invoke(utteranceId);
                }
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, linked.Token);
            }
            finally
            {
                _active--;
            }
        }

        public async UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _flush.Token);
            _active++;
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
                FirstSamplePlayed?.Invoke(utteranceId);
                await UniTask.Delay(TimeSpan.FromSeconds(clip.length), DelayType.Realtime, PlayerLoopTiming.Update, linked.Token);
            }
            finally
            {
                _active--;
            }
        }

        public void Flush()
        {
            FlushCount++;
            var flush = _flush;
            _flush = new CancellationTokenSource();
            flush.Cancel();
            flush.Dispose();
        }
    }
}
