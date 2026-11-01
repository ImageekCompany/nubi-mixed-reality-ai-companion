using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Audio.Output
{
    /// <summary>
    /// Main-thread half of the speech output. Every <see cref="Play"/> starts reading its stream at once (so the next
    /// sentence prefetches while the current one plays) but writes to the ring strictly in call order. Completion and
    /// <see cref="FirstSamplePlayed"/> are decided in <see cref="Tick"/> from the position the audio thread consumed,
    /// so call Tick once per frame.
    /// </summary>
    public sealed class SpeechOutput : ISpeechOutput, IDisposable
    {
        sealed class Utterance
        {
            public readonly Queue<PcmChunk> Pending = new();
            public readonly UniTaskCompletionSource Played = new();
            public int Id;
            public int Offset;
            public bool SourceDone;
            public bool FirstRaised;
            public bool Removed;
            public long Start = -1;
            public long End = -1;
        }

        readonly SpeechRingPlayer _player;
        readonly List<Utterance> _utterances = new();
        readonly List<Utterance> _flushing = new();
        readonly float[] _convert = new float[4096];
        CancellationTokenSource _flushCts = new();
        bool _disposed;

        public SpeechOutput(SpeechRingPlayer player) => _player = player;

        public bool IsPlaying => _utterances.Count > 0;

        public event Action<int> FirstSamplePlayed;

        public async UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SpeechOutput));
            ct.ThrowIfCancellationRequested();
            var utterance = new Utterance { Id = utteranceId };
            _utterances.Add(utterance);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _flushCts.Token);
            try
            {
                await foreach (var chunk in pcm.WithCancellation(linked.Token))
                {
                    utterance.Pending.Enqueue(chunk);
                    if (chunk.SampleRate != _player.SourceSampleRate)
                        throw new ArgumentException($"Speech must be {_player.SourceSampleRate} Hz, got {chunk.SampleRate} Hz.", nameof(pcm));
                    Pump();
                }
                utterance.SourceDone = true;
                Pump();
                using (linked.Token.Register(static s => ((Utterance)s).Played.TrySetCanceled(), utterance))
                    await utterance.Played.Task;
            }
            catch
            {
                Abandon(utterance);
                throw;
            }
        }

        public UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            if (ct.IsCancellationRequested)
                return UniTask.FromCanceled(ct);
            var chunk = ClipPcm.ToChunk(clip, _player.SourceSampleRate);
            return Play(UniTaskAsyncEnumerable.Return(chunk), utteranceId, ct);
        }

        public void Flush()
        {
            if (_disposed)
                return;
            // Before the cancellations: their continuations may already write the next utterance, which must survive.
            _player.DiscardQueued();
            _flushing.AddRange(_utterances);
            _utterances.Clear();
            foreach (var utterance in _flushing)
                Abandon(utterance);

            var flushed = _flushCts;
            _flushCts = new CancellationTokenSource();
            flushed.Cancel();
            flushed.Dispose();
            foreach (var utterance in _flushing)
                utterance.Played.TrySetCanceled();
            _flushing.Clear();
        }

        public void Tick()
        {
            Pump();
            var consumed = _player.Consumed;
            while (_utterances.Count > 0)
            {
                var utterance = _utterances[0];
                var finished = utterance.End >= 0 && consumed >= utterance.End;
                if (utterance.Start < 0 || (consumed <= utterance.Start && !finished))
                    return;

                if (!utterance.FirstRaised)
                {
                    utterance.FirstRaised = true;
                    FirstSamplePlayed?.Invoke(utterance.Id);
                    if (utterance.Removed)
                        continue;
                }
                if (!finished)
                    return;

                _utterances.RemoveAt(0);
                utterance.Removed = true;
                utterance.Played.TrySetResult();
            }
        }

        public void Dispose()
        {
            Flush();
            _flushCts.Dispose();
            _disposed = true;
        }

        void Pump()
        {
            foreach (var utterance in _utterances)
            {
                if (utterance.End >= 0)
                    continue;
                if (!WritePending(utterance) || !utterance.SourceDone)
                    return;
                if (utterance.Start < 0)
                    utterance.Start = _player.Written;
                utterance.End = _player.Written;
            }
        }

        /// <summary>False when the ring is full before the utterance's pending chunks are all written.</summary>
        bool WritePending(Utterance utterance)
        {
            while (utterance.Pending.Count > 0)
            {
                var chunk = utterance.Pending.Peek();
                if (utterance.Start < 0)
                    utterance.Start = _player.Written;

                var count = Math.Min(chunk.Length - utterance.Offset, Math.Min(_player.FreeSpace, _convert.Length));
                if (count == 0)
                    return false;
                var source = chunk.Buffer;
                for (var i = 0; i < count; i++)
                    _convert[i] = source[utterance.Offset + i] * (1f / 32768f);
                _player.Write(new ReadOnlySpan<float>(_convert, 0, count));

                utterance.Offset += count;
                if (utterance.Offset == chunk.Length)
                {
                    utterance.Pending.Dequeue().Dispose();
                    utterance.Offset = 0;
                }
            }
            return true;
        }

        void Abandon(Utterance utterance)
        {
            if (!utterance.Removed)
            {
                utterance.Removed = true;
                _utterances.Remove(utterance);
            }
            while (utterance.Pending.Count > 0)
                utterance.Pending.Dequeue().Dispose();
        }
    }
}
