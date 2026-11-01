using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Speech
{
    /// <summary>
    /// Speaks sentences in order through TTS and the speech output, synthesizing sentence n+1 while n plays
    /// (at most two requests in flight, ADR-0006). Main thread only.
    /// </summary>
    public sealed class SpeechQueue : IDisposable
    {
        const int MaxInFlight = 2;
        const int MinWitSpeed = 50;
        const int MaxWitSpeed = 200;

        readonly struct Item
        {
            public Item(string text, VoiceSpec voice, int utteranceId) { Text = text; Voice = voice; UtteranceId = utteranceId; }
            public string Text { get; }
            public VoiceSpec Voice { get; }
            public int UtteranceId { get; }
        }

        readonly ITextToSpeech _tts;
        readonly ISpeechOutput _output;
        readonly IUserPreferences _preferences;
        readonly Queue<Item> _pending = new();
        readonly Dictionary<int, string> _unstarted = new();
        readonly StringBuilder _spoken = new();
        CancellationTokenSource _cts = new();
        UniTaskCompletionSource _drained;
        int _inFlight;
        int _generation;
        bool _resetSpokenOnEnqueue;
        bool _disposed;

        public SpeechQueue(ITextToSpeech tts, ISpeechOutput output, IUserPreferences preferences)
        {
            _tts = tts;
            _output = output;
            _preferences = preferences;
            _output.FirstSamplePlayed += OnFirstSamplePlayed;
        }

        /// <summary>utteranceId, sentence; raised on the main thread when the sentence's first sample is played (captions).</summary>
        public event Action<int, string> SentenceStarted;

        /// <summary>The sentence was skipped because synthesis failed; the queue carries on with the next one.</summary>
        public event Action<int, ProviderException> SentenceFailed;

        /// <summary>
        /// Sentences whose audio has started, since the first Enqueue after the last <see cref="Clear"/>. It survives
        /// Clear, so a barge-in can still commit the spoken prefix; call Clear at the start of each turn.
        /// </summary>
        public string SpokenSoFar => _spoken.ToString();

        public void Enqueue(string sentence, VoiceSpec voice, int utteranceId)
        {
            if (_disposed || string.IsNullOrWhiteSpace(sentence))
                return;
            if (_resetSpokenOnEnqueue)
            {
                _spoken.Clear();
                _resetSpokenOnEnqueue = false;
            }

            _pending.Enqueue(new Item(sentence, WithUserRate(voice), utteranceId));
            _unstarted[utteranceId] = sentence;
            Pump();
        }

        public UniTask WhenDrained(CancellationToken ct)
        {
            if (IsIdle)
                return UniTask.CompletedTask;
            _drained ??= new UniTaskCompletionSource();
            return _drained.Task.AttachExternalCancellation(ct);
        }

        /// <summary>Cancels in-flight synthesis and playback and drops queued sentences.</summary>
        public void Clear()
        {
            _generation++;
            var cancelled = _cts;
            _cts = new CancellationTokenSource();
            cancelled.Cancel();
            cancelled.Dispose();
            _inFlight = 0;
            _pending.Clear();
            _unstarted.Clear();
            _resetSpokenOnEnqueue = true;
            SignalIfDrained();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _output.FirstSamplePlayed -= OnFirstSamplePlayed;
            Clear();
            _cts.Dispose();
            _disposed = true;
        }

        bool IsIdle => _inFlight == 0 && _pending.Count == 0;

        VoiceSpec WithUserRate(VoiceSpec voice)
        {
            var speed = Math.Clamp((int)Math.Round(voice.Speed * _preferences.Current.SpeechRate), MinWitSpeed, MaxWitSpeed);
            if (speed == voice.Speed)
                return voice;
            return new VoiceSpec { VoiceId = voice.VoiceId, Style = voice.Style, Pitch = voice.Pitch, Speed = speed };
        }

        void Pump()
        {
            while (_inFlight < MaxInFlight && _pending.Count > 0)
            {
                _inFlight++;
                Speak(_pending.Dequeue(), _generation, _cts.Token).Forget();
            }
        }

        async UniTaskVoid Speak(Item item, int generation, CancellationToken ct)
        {
            try
            {
                await _output.Play(_tts.Synthesize(item.Text, item.Voice, ct), item.UtteranceId, ct);
            }
            catch (OperationCanceledException)
            {
                // A flush from outside (barge-in) silences everything, so the sentences behind this one must not play.
                if (generation == _generation)
                    _pending.Clear();
            }
            catch (ProviderException e)
            {
                SentenceFailed?.Invoke(item.UtteranceId, e);
            }
            finally
            {
                if (generation == _generation)
                {
                    _unstarted.Remove(item.UtteranceId);
                    _inFlight--;
                    Pump();
                    SignalIfDrained();
                }
            }
        }

        void SignalIfDrained()
        {
            if (!IsIdle || _drained == null)
                return;
            var drained = _drained;
            _drained = null;
            drained.TrySetResult();
        }

        void OnFirstSamplePlayed(int utteranceId)
        {
            if (!_unstarted.Remove(utteranceId, out var sentence))
                return;
            if (_spoken.Length > 0)
                _spoken.Append(' ');
            _spoken.Append(sentence);
            SentenceStarted?.Invoke(utteranceId, sentence);
        }
    }
}
