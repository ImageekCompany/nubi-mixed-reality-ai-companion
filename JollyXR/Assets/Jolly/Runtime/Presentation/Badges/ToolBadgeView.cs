using System.Globalization;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>
    /// Shows which tool and which Meta model just ran, with its outcome and duration: how the video shows the tooling.
    /// Badges are a fixed pool of slots; the oldest one is reused when all are busy.
    /// </summary>
    public sealed class ToolBadgeView : MonoBehaviour
    {
        [SerializeField] ToolBadge[] _slots;
        [SerializeField] float _holdSeconds = 4f;

        IConversationEvents _events;
        ILocalizer _localizer;
        IClock _clock;
        string _runningText;
        string _okText;
        string _failedText;

        public ToolBadge[] Slots => _slots;

        public void Construct(IConversationEvents events, ILocalizer localizer, IClock clock)
        {
            Unsubscribe();
            _events = events;
            _localizer = localizer;
            _clock = clock;
            _events.ToolStarted += OnToolStarted;
            _events.ToolCompleted += OnToolCompleted;
            _events.ModelCallCompleted += OnModelCallCompleted;
            _localizer.Changed += Localize;
            Localize();
            foreach (var slot in _slots)
                slot.Hide();
        }

        void OnToolStarted(string callId, string toolName) =>
            Take().Show(callId, UiText.Get(_localizer, "tool." + toolName), _runningText, BadgeState.Running, double.PositiveInfinity);

        void OnToolCompleted(string callId, string toolName, bool ok)
        {
            var hideAt = _clock.Now + _holdSeconds;
            var detail = ok ? _okText : _failedText;
            var state = ok ? BadgeState.Succeeded : BadgeState.Failed;
            var slot = Find(callId);
            if (slot == null)
                Take().Show(callId, UiText.Get(_localizer, "tool." + toolName), detail, state, hideAt);
            else
                slot.Set(detail, state, hideAt);
        }

        void OnModelCallCompleted(string model, double ms) =>
            Take().Show(null, model, ms.ToString("0", CultureInfo.InvariantCulture) + " ms", BadgeState.Succeeded,
                _clock.Now + _holdSeconds);

        ToolBadge Find(string callId)
        {
            foreach (var slot in _slots)
            {
                if (slot.IsVisible && slot.CallId == callId)
                    return slot;
            }
            return null;
        }

        /// <summary>A hidden slot if any, else the one closest to expiry (running badges expire last).</summary>
        ToolBadge Take()
        {
            ToolBadge best = null;
            foreach (var slot in _slots)
            {
                if (!slot.IsVisible)
                    return slot;
                if (best == null || slot.HideAt < best.HideAt)
                    best = slot;
            }
            return best;
        }

        void Update()
        {
            if (_clock == null)
                return;
            var now = _clock.Now;
            foreach (var slot in _slots)
            {
                if (slot.IsVisible && now >= slot.HideAt)
                    slot.Hide();
            }
        }

        void Localize()
        {
            _runningText = UiText.Get(_localizer, "badge.running");
            _okText = UiText.Get(_localizer, "badge.ok");
            _failedText = UiText.Get(_localizer, "badge.failed");
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_events != null)
            {
                _events.ToolStarted -= OnToolStarted;
                _events.ToolCompleted -= OnToolCompleted;
                _events.ModelCallCompleted -= OnModelCallCompleted;
            }
            if (_localizer != null)
                _localizer.Changed -= Localize;
            _events = null;
            _localizer = null;
        }
    }
}
