using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    /// <summary>Label, value and large ◀ ▶ buttons (poke- and ray-friendly; no sliders to drag with a pinch).</summary>
    public sealed class SettingRow : MonoBehaviour
    {
        [SerializeField] TMP_Text _label;
        [SerializeField] TMP_Text _value;
        [SerializeField] Button _previous;
        [SerializeField] Button _next;

        public event Action<int> Stepped;

        public string Value => _value.text;
        public Button Previous => _previous;
        public Button Next => _next;

        void Awake()
        {
            _previous.onClick.AddListener(StepBack);
            _next.onClick.AddListener(StepForward);
        }

        void OnDestroy()
        {
            _previous.onClick.RemoveListener(StepBack);
            _next.onClick.RemoveListener(StepForward);
        }

        public void SetTexts(string label, string value)
        {
            _label.text = label;
            _value.text = value;
        }

        void StepBack() => Stepped?.Invoke(-1);
        void StepForward() => Stepped?.Invoke(1);
    }
}
