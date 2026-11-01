using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Offline
{
    /// <summary>ISpeechOutput for clips only. A clip "plays" until the test calls <see cref="StartCurrent"/>
    /// (first sample) and <see cref="FinishCurrent"/> (last sample).</summary>
    public sealed class FakeSpeechOutput : ISpeechOutput
    {
        readonly List<(AudioClip clip, int utteranceId)> _played = new();
        UniTaskCompletionSource _current;
        int _currentId;

        public IReadOnlyList<(AudioClip clip, int utteranceId)> Played => _played;
        public bool IsPlaying => _current != null;
        public bool HasSubscribers => FirstSamplePlayed != null;

        public event Action<int> FirstSamplePlayed;

        public UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct)
        {
            _played.Add((clip, utteranceId));
            _currentId = utteranceId;
            var source = _current = new UniTaskCompletionSource();
            ct.Register(() =>
            {
                if (source.TrySetCanceled(ct) && _current == source)
                    _current = null;
            });
            return source.Task;
        }

        public void StartCurrent() => FirstSamplePlayed?.Invoke(_currentId);

        public void RaiseFirstSample(int utteranceId) => FirstSamplePlayed?.Invoke(utteranceId);

        public void FinishCurrent()
        {
            var source = _current;
            _current = null;
            source.TrySetResult();
        }

        public UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct) =>
            throw new NotSupportedException("FakeSpeechOutput plays clips only.");

        public void Flush()
        {
            var source = _current;
            _current = null;
            source?.TrySetCanceled();
        }
    }
}
