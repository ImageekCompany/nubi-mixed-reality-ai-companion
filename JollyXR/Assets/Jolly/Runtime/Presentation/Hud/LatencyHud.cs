using System.Globalization;
using System.Text;
using Jolly.Contracts;
using TMPro;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>
    /// Per-turn latency breakdown from ILatencyTracker (the same marks as the evt=turn.latency log line), refreshed when
    /// a turn returns to Idle, plus the latest Meta model call durations.
    /// </summary>
    public sealed class LatencyHud : MonoBehaviour
    {
        struct Row
        {
            public string Key;
            public LatencyMark From;
            public LatencyMark To;
        }

        static readonly Row[] Rows =
        {
            new() { Key = "hud.release_to_audio", From = LatencyMark.PttReleased, To = LatencyMark.FirstAudioPlayed },
            new() { Key = "hud.asr_final", From = LatencyMark.PttReleased, To = LatencyMark.AsrFinal },
            new() { Key = "hud.llm_first_text", From = LatencyMark.AsrFinal, To = LatencyMark.LlmFirstText },
            new() { Key = "hud.tts_first_byte", From = LatencyMark.FirstSentenceQueued, To = LatencyMark.TtsFirstByte },
            new() { Key = "hud.mask_shown", From = LatencyMark.PttReleased, To = LatencyMark.MaskShown },
        };

        [SerializeField] TMP_Text _text;
        [SerializeField] int _modelRows = 3;

        readonly StringBuilder _builder = new(256);
        IConversationEvents _events;
        ILatencyTracker _tracker;
        ILocalizer _localizer;
        string _title;
        string[] _labels;
        string[] _models;
        double[] _modelMs;
        int _modelCount;

        public string Text => _text.text;

        public void Construct(IConversationEvents events, ILatencyTracker tracker, ILocalizer localizer)
        {
            Unsubscribe();
            _events = events;
            _tracker = tracker;
            _localizer = localizer;
            _models = new string[_modelRows];
            _modelMs = new double[_modelRows];
            _modelCount = 0;
            _events.StateChanged += OnStateChanged;
            _events.ModelCallCompleted += OnModelCallCompleted;
            _localizer.Changed += Localize;
            Localize();
        }

        void OnStateChanged(ConversationState from, ConversationState to)
        {
            if (to == ConversationState.Idle)
                Render();
        }

        /// <summary>Newest first; the oldest drops off.</summary>
        void OnModelCallCompleted(string model, double ms)
        {
            for (var i = Mathf.Min(_modelCount, _modelRows - 1); i > 0; i--)
            {
                _models[i] = _models[i - 1];
                _modelMs[i] = _modelMs[i - 1];
            }
            _models[0] = ModelLabels.Of(model, _localizer);
            _modelMs[0] = ms;
            _modelCount = Mathf.Min(_modelCount + 1, _modelRows);
            Render();
        }

        void Localize()
        {
            _title = UiText.Get(_localizer, "hud.title");
            _labels = new string[Rows.Length];
            for (var i = 0; i < Rows.Length; i++)
                _labels[i] = UiText.Get(_localizer, Rows[i].Key);
            Render();
        }

        void Render()
        {
            _builder.Clear().Append("<b>").Append(_title).Append("</b>");
            for (var i = 0; i < Rows.Length; i++)
                AppendLine(_labels[i], _tracker.Between(Rows[i].From, Rows[i].To));
            for (var i = 0; i < _modelCount; i++)
                AppendLine(_models[i], _modelMs[i]);
            _text.SetText(_builder);
        }

        void AppendLine(string label, double ms)
        {
            _builder.Append('\n').Append(label).Append(": ");
            if (double.IsNaN(ms))
                _builder.Append('–');
            else
                _builder.Append(ms.ToString("0", CultureInfo.InvariantCulture));
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_events != null)
            {
                _events.StateChanged -= OnStateChanged;
                _events.ModelCallCompleted -= OnModelCallCompleted;
            }
            if (_localizer != null)
                _localizer.Changed -= Localize;
            _events = null;
            _localizer = null;
        }
    }
}
