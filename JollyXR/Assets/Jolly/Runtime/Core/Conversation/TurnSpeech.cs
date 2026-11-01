using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Turns a turn's speech deltas into queued sentences and the speech side of the state machine: SpeechQueued per
    /// sentence, FirstAudio + caption when a sentence's first sample plays, SpeechDrained when the queue empties. When the
    /// locale has no TTS voice, sentences become captions only (architecture §10).
    /// </summary>
    public sealed class TurnSpeech : IDisposable
    {
        readonly ISpeechQueue _queue;
        readonly ISentenceSplitter _splitter;
        readonly ISpeechOutput _output;
        readonly ConversationStateMachine _machine;
        readonly ConversationEvents _events;
        readonly ILatencyTracker _latency;
        readonly Dictionary<int, string> _awaitingFirstSample = new();
        int _nextUtteranceId;
        int _turnId;
        VoiceSpec _voice;
        bool _ttsAvailable;
        CancellationToken _ct;
        UniTaskCompletionSource _drain;

        public TurnSpeech(ISpeechQueue queue, ISentenceSplitter splitter, ISpeechOutput output,
                          ConversationStateMachine machine, ConversationEvents events, ILatencyTracker latency)
        {
            _queue = queue;
            _splitter = splitter;
            _output = output;
            _machine = machine;
            _events = events;
            _latency = latency;
            _output.FirstSamplePlayed += OnFirstSamplePlayed;
        }

        public int NextUtteranceId() => ++_nextUtteranceId;

        public void Begin(int turnId, VoiceSpec voice, bool ttsAvailable, CancellationToken ct)
        {
            _splitter.Flush();
            _awaitingFirstSample.Clear();
            _drain = null;
            _turnId = turnId;
            _voice = voice;
            _ttsAvailable = ttsAvailable;
            _ct = ct;
        }

        public void Append(string delta)
        {
            _splitter.Append(delta);
            while (_splitter.TryTake(out var sentence))
                Speak(sentence);
        }

        public void Finish() => Speak(_splitter.Flush());

        /// <summary>Completes once everything queued in this turn has played (at once when nothing is queued).</summary>
        public UniTask WhenIdle() => _drain?.Task ?? UniTask.CompletedTask;

        public void Dispose() => _output.FirstSamplePlayed -= OnFirstSamplePlayed;

        void Speak(string sentence)
        {
            sentence = sentence?.Trim();
            if (string.IsNullOrEmpty(sentence))
                return;
            var id = NextUtteranceId();
            if (!_ttsAvailable)
            {
                _events.RaiseCaptionStarted(id, sentence);
                return;
            }
            _awaitingFirstSample[id] = sentence;
            _latency.Mark(_turnId, LatencyMark.FirstSentenceQueued);
            _machine.Fire(ConversationTrigger.SpeechQueued);
            _queue.Enqueue(sentence, _voice, id);
            if (_drain == null)
                WatchDrain().Forget();
        }

        async UniTaskVoid WatchDrain()
        {
            var drain = _drain = new UniTaskCompletionSource();
            var cancelled = await _queue.WhenDrained(_ct).SuppressCancellationThrow();
            if (_drain == drain)
                _drain = null;
            if (!cancelled)
                _machine.Fire(ConversationTrigger.SpeechDrained);
            drain.TrySetResult();
        }

        void OnFirstSamplePlayed(int utteranceId)
        {
            if (!_awaitingFirstSample.Remove(utteranceId, out var sentence))
                return;
            _latency.Mark(_turnId, LatencyMark.FirstAudioPlayed);
            _machine.Fire(ConversationTrigger.FirstAudio);
            _events.RaiseCaptionStarted(utteranceId, sentence);
        }
    }
}
