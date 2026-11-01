using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    /// <summary>
    /// One large button per option (persona, language): a single tap chooses, so a persona switch never passes
    /// through the personas in between. The selected button stays non-interactable, which also marks it visually.
    /// </summary>
    public sealed class ChoiceGroup : MonoBehaviour
    {
        [SerializeField] TMP_Text _label;
        [SerializeField] Button _template;
        [SerializeField] Transform _container;

        readonly List<Button> _buttons = new();
        readonly List<string> _ids = new();

        public event Action<string> Chosen;

        public IReadOnlyList<Button> Buttons => _buttons;

        public void Build(IReadOnlyList<string> ids)
        {
            foreach (var b in _buttons)
                Destroy(b.gameObject);
            _buttons.Clear();
            _ids.Clear();
            _template.gameObject.SetActive(false);

            foreach (var id in ids)
            {
                var button = Instantiate(_template, _container);
                button.gameObject.name = id;
                button.gameObject.SetActive(true);
                var chosenId = id;
                button.onClick.AddListener(() => Chosen?.Invoke(chosenId));
                _buttons.Add(button);
                _ids.Add(id);
            }
        }

        public void SetLabel(string label) => _label.text = label;

        public void SetOptionText(int index, string text) => _buttons[index].GetComponentInChildren<TMP_Text>().text = text;

        public string IdAt(int index) => _ids[index];

        public void Select(string id)
        {
            for (var i = 0; i < _buttons.Count; i++)
                _buttons[i].interactable = _ids[i] != id;
        }
    }
}
