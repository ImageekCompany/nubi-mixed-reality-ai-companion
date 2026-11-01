using System;
using Jolly.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    /// <summary>
    /// The small settings button on the wrist (ADR-0042), pressed with a poke or a ray. Where and when it shows is the
    /// XR side's call (<c>WristAnchor</c>); hidden, its canvas and raycaster are off, so it neither draws nor catches a ray.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WristMenuButton : MonoBehaviour
    {
        [SerializeField] Canvas _canvas;
        [SerializeField] BaseRaycaster _raycaster;
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _label;

        ILocalizer _localizer;

        public event Action Pressed;

        void Awake() => SetVisible(false);

        public void Construct(ILocalizer localizer)
        {
            Unsubscribe();
            _localizer = localizer;
            _button.onClick.AddListener(OnClicked);
            _localizer.Changed += Localize;
            Localize();
        }

        public void SetVisible(bool visible)
        {
            _canvas.enabled = visible;
            _raycaster.enabled = visible;
        }

        void OnClicked() => Pressed?.Invoke();

        void Localize() => _label.text = UiText.Get(_localizer, "settings.title");

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_localizer == null)
                return;
            _button.onClick.RemoveListener(OnClicked);
            _localizer.Changed -= Localize;
            _localizer = null;
        }
    }
}
