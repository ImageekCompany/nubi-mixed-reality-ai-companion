using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    public enum BadgeState { Running, Succeeded, Failed }

    /// <summary>One badge slot, one line: a title (tool or model) and a detail (outcome or duration), with the state shown by colour.</summary>
    public sealed class ToolBadge : MonoBehaviour
    {
        [SerializeField] Image _background;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _detail;
        [SerializeField] Color _running = new(0.66f, 0.41f, 0.04f, 0.92f);
        [SerializeField] Color _succeeded = new(0.11f, 0.5f, 0.3f, 0.92f);
        [SerializeField] Color _failed = new(0.72f, 0.2f, 0.2f, 0.92f);

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
