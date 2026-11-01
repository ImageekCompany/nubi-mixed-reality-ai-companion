using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>
    /// Records sentences. <see cref="PlayNext"/> starts the oldest unplayed one (raising its first sample),
    /// <see cref="Drain"/> plays everything and completes <see cref="WhenDrained"/>, and <see cref="DrainUnplayed"/>
    /// completes it without playing the rest (TTS failed).
    /// </summary>
    public sealed class RecordingSpeechQueue : ISpeechQueue
    {
        readonly FakeSpeechOutput _output;
        UniTaskCompletionSource _drained;
        int _played;

        public RecordingSpeechQueue(FakeSpeechOutput output) => _output = output;

        public List<(string sentence, VoiceSpec voice, int utteranceId)> Enqueued { get; } = new();
        public int Clears { get; private set; }
        public int Pending => Enqueued.Count - _played;

        public void Enqueue(string sentence, VoiceSpec voice, int utteranceId) => Enqueued.Add((sentence, voice, utteranceId));

        public UniTask WhenDrained(CancellationToken ct)
        {
            if (Pending == 0)
                return UniTask.CompletedTask;
            _drained ??= new UniTaskCompletionSource();
            return _drained.Task.AttachExternalCancellation(ct);
        }

        public void Clear()
        {
            Clears++;
            _played = Enqueued.Count;
            Complete();
        }

        public void PlayNext()
        {
            _output.RaiseFirstSample(Enqueued[_played++].utteranceId);
        }

        public void Drain()
        {
            while (Pending > 0)
                PlayNext();
            Complete();
        }

        public void DrainUnplayed()
        {
            _played = Enqueued.Count;
            Complete();
        }

        void Complete()
        {
            var drained = _drained;
            _drained = null;
            drained?.TrySetResult();
        }
    }

    /// <summary>Splits after '.', '?' or '!'.</summary>
    public sealed class FakeSentenceSplitter : ISentenceSplitter
    {
        readonly StringBuilder _buffer = new();

        public void Append(string delta) => _buffer.Append(delta);

        public bool TryTake(out string sentence)
        {
            for (var i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] is not ('.' or '?' or '!'))
                    continue;
                sentence = _buffer.ToString(0, i + 1);
                _buffer.Remove(0, i + 1);
                return true;
            }
            sentence = null;
            return false;
        }

        public string Flush()
        {
            var rest = _buffer.ToString();
            _buffer.Clear();
            return rest;
        }
    }

    /// <summary>
    /// Records canned-line requests and answers each with <see cref="TextOf"/> (no clip). With <see cref="Output"/> set, a
    /// request raises its first sample there, as a played clip would. With <see cref="Hold"/> each waits for
    /// <see cref="Release"/> (or its token).
    /// </summary>
    public sealed class RecordingOfflineResponder : IOfflineResponder
    {
        readonly Queue<UniTaskCompletionSource> _held = new();

        public List<LineCategory> Categories { get; } = new();
        public bool Hold { get; set; }
        public FakeSpeechOutput Output { get; set; }
        public int Waiting => _held.Count;

        public static string TextOf(LineCategory category) => category + " line";

        public async UniTask<CannedLine> Respond(LineCategory category, int utteranceId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Categories.Add(category);
            Output?.RaiseFirstSample(utteranceId);
            if (Hold)
            {
                var gate = new UniTaskCompletionSource();
                _held.Enqueue(gate);
                await gate.Task.AttachExternalCancellation(ct);
            }
            return new CannedLine(category.ToString(), TextOf(category), null);
        }

        public void Release() => _held.Dequeue().TrySetResult();
    }
}
