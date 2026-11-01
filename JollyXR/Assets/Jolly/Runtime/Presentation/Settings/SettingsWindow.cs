using Jolly.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    /// <summary>
    /// Shows the settings panel only on demand (ADR-0042): hidden at launch; opened from the wrist button, the menu
    /// button or a voice command, placed once in front of the user and then left where it is in the room. It closes on
    /// its X, on the same controls again, when a talk turn starts, and after a while with no control used. Hiding turns
    /// off the canvas and its raycaster, so the hidden panel neither draws nor catches a ray, and
    /// <see cref="SettingsPanel"/> keeps serving the preferences.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsWindow : MonoBehaviour
    {
        [SerializeField] SettingsPanel _panel;
        [SerializeField] Canvas _canvas;
        [SerializeField] BaseRaycaster _raycaster;
        [SerializeField] Button _close;
        [Tooltip("Horizontal metres from the eyes when it opens.")]
        [SerializeField] float _distance = 0.7f;
        [Tooltip("Metres the panel's centre sits below eye height when it opens.")]
        [SerializeField] float _drop = 0.15f;
        [Tooltip("Closes after this many seconds without a control on it being used.")]
        [SerializeField] float _idleSeconds = 20f;

        PanelVisibility _visibility;
        IClock _clock;
        Transform _head;
        ILog _log;

        public bool IsOpen => _visibility != null && _visibility.IsOpen;

        void Awake() => Show(false);

        public void Construct(IClock clock, Transform head, ILog log)
        {
            Unsubscribe();
            _clock = clock;
            _head = head;
            _log = log;
            _visibility = new PanelVisibility(_idleSeconds);
            _panel.Interacted += OnInteracted;
            _close.onClick.AddListener(OnCloseClicked);
            Show(false);
        }

        /// <summary>Shows it in front of the user; if it is already open, brings it back there.</summary>
        public void Open(string source)
        {
            if (_visibility.Open(_clock.Now))
                _log.Write(LogLevel.Info, "settings", "evt=settings.open source=" + source);
            var pose = PanelPlacement.InFrontOf(_head.position, _head.forward, _distance, _drop);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            Show(true);
        }

        public void Close(string reason)
        {
            if (_visibility == null || !_visibility.Close())
                return;
            Show(false);
            _log.Write(LogLevel.Info, "settings", "evt=settings.close reason=" + reason);
        }

        public void Toggle(string source)
        {
            if (IsOpen)
                Close(source);
            else
                Open(source);
        }

        void Update()
        {
            if (_visibility != null && _visibility.IsIdle(_clock.Now))
                Close("idle");
        }

        void OnInteracted() => _visibility.Touch(_clock.Now);

        void OnCloseClicked() => Close("button");

        void Show(bool shown)
        {
            _canvas.enabled = shown;
            _raycaster.enabled = shown;
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_visibility == null)
                return;
            _panel.Interacted -= OnInteracted;
            _close.onClick.RemoveListener(OnCloseClicked);
            _visibility = null;
        }
    }
}
