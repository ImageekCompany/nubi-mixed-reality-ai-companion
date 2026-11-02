using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Speech;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Turns a turn's speech deltas into queued sentences and the speech side of the state machine: SpeechQueued per
    /// sentence, FirstAudio + caption when a sentence's first sample plays, SpeechDrained when the queue empties. When the
    /// locale has no TTS voice, sentences become captions only (architecture §10), each held for its reading time so a
    /// reply is not reduced to its last sentence; the first one counts as FirstAudio. It also keeps what this turn has
    /// said, for the agent's interrupted commit: the queue's own record spans turns and is wiped by the barge-in's Clear.
    /// </summary>
    public sealed class TurnSpeech : IDisposable
    {
        readonly ISpeechQueue _queue;
        readonly ISentenceSplitter _splitter;
        readonly ISpeechOutput _output;
        readonly ConversationStateMachine _machine;
        readonly ConversationEvents _events;
        readonly ILatencyTracker _latency;
        readonly IClock _clock;
        readonly Dictionary<int, string> _awaitingFirstSample = new();
        readonly Queue<(int Id, string Text)> _captionsOnly = new();
        readonly StringBuilder _spoken = new();
        int _nextUtteranceId;
        int _turnId;
        int _mustCaptionId;
        int _clipId;
        VoiceSpec _voice;
        bool _ttsAvailable;
        CancellationToken _ct;
        UniTaskCompletionSource _drain;
        bool _pacing;

        public TurnSpeech(ISpeechQueue queue, ISentenceSplitter splitter, ISpeechOutput output,
                          ConversationStateMachine machine, ConversationEvents events, ILatencyTracker latency, IClock clock)
        {
            _clock = clock;
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
            _captionsOnly.Clear();
            _spoken.Clear();
            _mustCaptionId = 0;
            _clipId = 0;
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

        /// <summary>
        /// Speaks <paramref name="text"/> as one utterance after whatever is buffered. Its caption follows its first sample
        /// like any other, but if the queue drains without playing it (TTS failed) it is captioned then (the persona safety
        /// notice must be shown).
        /// </summary>
        public void SpeakEnsuringCaption(string text)
        {
            Finish();
            _mustCaptionId = Speak(text);
        }

        /// <summary>
        /// The utterance id for a canned clip this turn plays through <see cref="IOfflineResponder"/>, which captions it:
        /// its first sample is the turn's FirstAudio, as a synthesized sentence's would be.
        /// </summary>
        public int ExpectClip() => _clipId = NextUtteranceId();

        /// <summary>Sentences captioned or started playing since <see cref="Begin"/>, in order.</summary>
        public string SpokenThisTurn => _spoken.ToString();

        /// <summary>A barge-in: a first sample that arrives after the flush belongs to silenced audio.</summary>
        public void Cancel()
        {
            _awaitingFirstSample.Clear();
            _clipId = 0;
        }

        /// <summary>Completes once everything queued in this turn has played (at once when nothing is queued).</summary>
        public UniTask WhenIdle() => _drain?.Task ?? UniTask.CompletedTask;

        public void Dispose() => _output.FirstSamplePlayed -= OnFirstSamplePlayed;

        /// <summary>The utterance id, or 0 when there was nothing to say or it was only captioned.</summary>
        int Speak(string sentence)
        {
            sentence = SpokenText.Clean(sentence?.Trim());
            if (string.IsNullOrEmpty(sentence))
                return 0;
            var id = NextUtteranceId();
            if (!_ttsAvailable)
            {
                _captionsOnly.Enqueue((id, sentence));
                _machine.Fire(ConversationTrigger.SpeechQueued);
                if (!_pacing)
                    PaceCaptions().Forget();
                return 0;
            }
            _awaitingFirstSample[id] = sentence;
            _latency.Mark(_turnId, LatencyMark.FirstSentenceQueued);
            _machine.Fire(ConversationTrigger.SpeechQueued);
            _queue.Enqueue(sentence, _voice, id);
            if (_drain == null)
                WatchDrain().Forget();
            return id;
        }

        async UniTaskVoid WatchDrain()
        {
            var drain = _drain = new UniTaskCompletionSource();
            var cancelled = await _queue.WhenDrained(_ct).SuppressCancellationThrow();
            if (_drain == drain)
                _drain = null;
            if (!cancelled)
            {
                if (_awaitingFirstSample.Remove(_mustCaptionId, out var unplayed))
                    Caption(_mustCaptionId, unplayed);
                _machine.Fire(ConversationTrigger.SpeechDrained);
            }
            drain.TrySetResult();
        }

        async UniTaskVoid PaceCaptions()
        {
            _pacing = true;
            var drain = _drain ??= new UniTaskCompletionSource();
            var ct = _ct;
            var first = true;
            try
            {
                while (_captionsOnly.Count > 0)
                {
                    var (id, text) = _captionsOnly.Dequeue();
                    if (first)
                        _machine.Fire(ConversationTrigger.FirstAudio);
                    first = false;
                    Caption(id, text);
                    if (await _clock.Delay(CaptionTiming.HoldFor(text), ct).SuppressCancellationThrow())
                        return;
                }
                _machine.Fire(ConversationTrigger.SpeechDrained);
            }
            finally
            {
                _pacing = false;
                _captionsOnly.Clear();
                if (_drain == drain)
                    _drain = null;
                drain.TrySetResult();
            }
        }

        void OnFirstSamplePlayed(int utteranceId)
        {
            if (utteranceId != 0 && utteranceId == _clipId)
            {
                _clipId = 0;
                MarkFirstAudio();
                return;
            }
            if (!_awaitingFirstSample.Remove(utteranceId, out var sentence))
                return;
            MarkFirstAudio();
            Caption(utteranceId, sentence);
        }

        void MarkFirstAudio()
        {
            _latency.Mark(_turnId, LatencyMark.FirstAudioPlayed);
            _machine.Fire(ConversationTrigger.FirstAudio);
        }

        void Caption(int utteranceId, string sentence)
        {
            _spoken.Append(sentence).Append(' ');
            _events.RaiseCaptionStarted(utteranceId, sentence);
        }
    }
}
