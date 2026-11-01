using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    public enum BadgeState { Running, Succeeded, Failed }

    /// <summary>One badge slot: a title (tool or model), a detail line and a state shown by colour and a glyph.</summary>
    public sealed class ToolBadge : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _detail;
        [SerializeField] Color _running = new(0.95f, 0.7f, 0.2f, 0.9f);
        [SerializeField] Color _succeeded = new(0.25f, 0.75f, 0.45f, 0.9f);
        [SerializeField] Color _failed = new(0.85f, 0.3f, 0.3f, 0.9f);

        public string CallId { get; private set; }
        public BadgeState State { get; private set; }
        public double HideAt { get; private set; } = double.PositiveInfinity;
        public string Title => _title.text;
        public string Detail => _detail.text;
        public bool IsVisible => gameObject.activeSelf;

        public void Show(string callId, string title, string detail, BadgeState state, double hideAt)
        {
            CallId = callId;
            _title.text = title;
            Set(detail, state, hideAt);
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        public void Set(string detail, BadgeState state, double hideAt)
        {
            State = state;
            HideAt = hideAt;
            _detail.text = detail;
            _background.color = state switch
            {
                BadgeState.Running => _running,
                BadgeState.Succeeded => _succeeded,
                _ => _failed,
            };
        }

        public void Hide()
        {
            CallId = null;
            HideAt = double.PositiveInfinity;
            gameObject.SetActive(false);
        }
    }
}
