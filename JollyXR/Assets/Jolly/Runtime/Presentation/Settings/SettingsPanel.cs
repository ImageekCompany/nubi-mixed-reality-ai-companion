using System;
using System.Globalization;
using Jolly.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jolly.Presentation
{
    /// <summary>
    /// World-space settings (accessibility, persona, language, memory, usage) and the app's IUserPreferences.
    /// Persona, language and clear-memory are requests, not actions: the composition root routes them to the
    /// conversation controller, which applies them between turns (architecture §3).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour, IUserPreferences
    {
        static readonly string[] CaptionSizeKeys = { "settings.size.small", "settings.size.medium", "settings.size.large" };

        [SerializeField] TMP_Text _title;
        [SerializeField] SettingRow _captionSize;
        [SerializeField] SettingRow _speechRate;
        [SerializeField] SettingRow _volume;
        [SerializeField] SettingRow _seated;
        [SerializeField] SettingRow _pttHand;
        [SerializeField] ChoiceGroup _personas;
        [SerializeField] ChoiceGroup _languages;
        [SerializeField] Button _clearMemory;
        [SerializeField] TMP_Text _clearMemoryLabel;
        [SerializeField] TMP_Text _usage;
        [Tooltip("Clear memory needs a second tap within this window.")]
        [SerializeField] float _confirmSeconds = 3f;

        ILocalizer _localizer;
        IPersonaProvider _personaProvider;
        IApiUsageMeter _usageMeter;
        IPreferencesStore _store;
        IClock _clock;
        double _confirmUntil = double.NegativeInfinity;

        public UserPreferencesSnapshot Current { get; private set; } = new();
        public event Action<UserPreferencesSnapshot> Changed;
        public event Action<string> PersonaSelected;
        public event Action<string> LanguageSelected;
        public event Action ClearMemoryRequested;

        public bool IsConfirmingClear => _clock != null && _clock.Now < _confirmUntil;

        public void Construct(ILocalizer localizer, IPersonaProvider personas, IApiUsageMeter usage,
                              IPreferencesStore store, IClock clock)
        {
            Unsubscribe();
            _localizer = localizer;
            _personaProvider = personas;
            _usageMeter = usage;
            _store = store;
            _clock = clock;
            Current = SettingsModel.Sanitize(store.Load());

            _personas.Build(personas.AvailableIds);
            _languages.Build(localizer.AvailableLanguageTags);
            Subscribe();
            Localize();
            OnPersonaChanged(personas.Current);
        }

        void Subscribe()
        {
            _captionSize.Stepped += OnCaptionSizeStepped;
            _speechRate.Stepped += OnSpeechRateStepped;
            _volume.Stepped += OnVolumeStepped;
            _seated.Stepped += OnSeatedStepped;
            _pttHand.Stepped += OnPttHandStepped;
            _personas.Chosen += OnPersonaChosen;
            _languages.Chosen += OnLanguageChosen;
            _clearMemory.onClick.AddListener(OnClearMemoryClicked);
            _localizer.Changed += Localize;
            _personaProvider.Changed += OnPersonaChanged;
            _usageMeter.SpentChanged += OnSpentChanged;
        }

        void OnCaptionSizeStepped(int direction) => Step(Setting.CaptionSize, direction);
        void OnSpeechRateStepped(int direction) => Step(Setting.SpeechRate, direction);
        void OnVolumeStepped(int direction) => Step(Setting.Volume, direction);
        void OnSeatedStepped(int direction) => Step(Setting.Seated, direction);
        void OnPttHandStepped(int direction) => Step(Setting.PttHand, direction);
        void OnPersonaChosen(string id) => PersonaSelected?.Invoke(id);
        void OnLanguageChosen(string tag) => LanguageSelected?.Invoke(tag);
        void OnSpentChanged(decimal spent) => RenderUsage();

        void Step(Setting setting, int direction)
        {
            Current = SettingsModel.Step(Current, setting, direction);
            _store.Save(Current);
            RenderRows();
            Changed?.Invoke(Current);
        }

        void OnClearMemoryClicked()
        {
            if (IsConfirmingClear)
            {
                _confirmUntil = double.NegativeInfinity;
                RenderClearMemory();
                ClearMemoryRequested?.Invoke();
                return;
            }
            _confirmUntil = _clock.Now + _confirmSeconds;
            RenderClearMemory();
        }

        void OnPersonaChanged(PersonaProfile persona)
        {
            if (persona != null)
                _personas.Select(persona.Id);
        }

        void Update()
        {
            if (_clock != null && _confirmUntil > double.NegativeInfinity && _clock.Now >= _confirmUntil)
            {
                _confirmUntil = double.NegativeInfinity;
                RenderClearMemory();
            }
        }

        void Localize()
        {
            _title.text = Text("settings.title");
            _personas.SetLabel(Text("settings.persona"));
            for (var i = 0; i < _personas.Buttons.Count; i++)
                _personas.SetOptionText(i, Text("persona." + _personas.IdAt(i)));
            _languages.SetLabel(Text("settings.language"));
            for (var i = 0; i < _languages.Buttons.Count; i++)
                _languages.SetOptionText(i, Text("language." + _languages.IdAt(i)));
            _languages.Select(_localizer.LanguageTag);
            RenderRows();
            RenderClearMemory();
            RenderUsage();
        }

        void RenderRows()
        {
            var p = Current;
            _captionSize.SetTexts(Text("settings.caption_size"),
                Text(CaptionSizeKeys[Mathf.Clamp(p.CaptionSize, 0, CaptionSizeKeys.Length - 1)]));
            _speechRate.SetTexts(Text("settings.speech_rate"), p.SpeechRate.ToString("0.##", CultureInfo.InvariantCulture) + "×");
            _volume.SetTexts(Text("settings.volume"), Mathf.RoundToInt(p.Volume * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            _seated.SetTexts(Text("settings.seated"), Text(p.Seated ? "settings.on" : "settings.off"));
            _pttHand.SetTexts(Text("settings.ptt_hand"), Text(p.PttEitherHand ? "settings.ptt.either" : "settings.ptt.dominant"));
        }

        void RenderClearMemory() =>
            _clearMemoryLabel.text = Text(IsConfirmingClear ? "settings.clear_memory.confirm" : "settings.clear_memory");

        void RenderUsage() =>
            _usage.text = Text("settings.usage") + ": $" + _usageMeter.SpentUsd.ToString("0.00", CultureInfo.InvariantCulture)
                          + " / $" + _usageMeter.HardCapUsd.ToString("0.00", CultureInfo.InvariantCulture);

        string Text(string key) => UiText.Get(_localizer, key);

        void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_localizer == null)
                return;
            _captionSize.Stepped -= OnCaptionSizeStepped;
            _speechRate.Stepped -= OnSpeechRateStepped;
            _volume.Stepped -= OnVolumeStepped;
            _seated.Stepped -= OnSeatedStepped;
            _pttHand.Stepped -= OnPttHandStepped;
            _personas.Chosen -= OnPersonaChosen;
            _languages.Chosen -= OnLanguageChosen;
            _clearMemory.onClick.RemoveListener(OnClearMemoryClicked);
            _localizer.Changed -= Localize;
            _personaProvider.Changed -= OnPersonaChanged;
            _usageMeter.SpentChanged -= OnSpentChanged;
            _localizer = null;
        }
    }
}
