using Jolly.Contracts;
using TMPro;
using UnityEngine;

namespace Jolly.Presentation
{
    /// <summary>Degraded-mode pill (API key, offline, budget, camera, mic). Hidden for <see cref="StatusKind.None"/>.</summary>
    public sealed class StatusIndicator : MonoBehaviour, IStatusSink
    {
        static readonly string[] Keys = UiText.KeysFor<StatusKind>("status.");

        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _label;

        ILocalizer _localizer;
        StatusKind _status;

        public StatusKind Current => _status;

        public void Construct(ILocalizer localizer)
        {
            Unsubscribe();
            _localizer = localizer;
            _localizer.Changed += Refresh;
            Refresh();
        }

        public void Show(StatusKind status)
        {
            _status = status;
            Refresh();
        }

        void Refresh()
        {
            var visible = _status != StatusKind.None && _localizer != null;
            _root.SetActive(visible);
            if (visible)
                _label.text = UiText.Get(_localizer, Keys[(int)_status]);
        }

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_localizer != null)
                _localizer.Changed -= Refresh;
            _localizer = null;
        }
    }
}
