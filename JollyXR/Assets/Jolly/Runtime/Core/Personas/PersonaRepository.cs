using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Personas
{
    /// <summary>
    /// Resolves <see cref="PersonaDefinition"/>s into <see cref="PersonaProfile"/>s for the current locale. A switch is only
    /// recorded by <see cref="RequestSwitch"/>; the controller applies it between turns (architecture §3).
    /// </summary>
    public sealed class PersonaRepository : IPersonaProvider, IPersonaDirectory, IDisposable
    {
        public const string Table = "Personas";

        readonly Dictionary<string, PersonaDefinition> _definitions = new(StringComparer.Ordinal);
        readonly List<string> _ids = new();
        readonly ILocalizer _localizer;

        public PersonaRepository(IReadOnlyList<PersonaDefinition> definitions, string defaultId, ILocalizer localizer)
        {
            _localizer = localizer;
            foreach (var d in definitions)
            {
                _definitions.Add(d.Id, d);
                _ids.Add(d.Id);
            }
            if (!_definitions.TryGetValue(defaultId, out var initial))
                throw new ArgumentException($"default persona '{defaultId}' is not defined");
            Current = Resolve(initial);
            _localizer.Changed += OnLocaleChanged;
        }

        public PersonaProfile Current { get; private set; }
        public IReadOnlyList<string> AvailableIds => _ids;
        public string PendingSwitchId { get; private set; }
        public event Action<PersonaProfile> Changed;
        public event Action SwitchRequested;

        public bool RequestSwitch(string id)
        {
            if (id == null || !_definitions.ContainsKey(id))
                return false;
            PendingSwitchId = id;
            SwitchRequested?.Invoke();
            return true;
        }

        public UniTask<bool> ApplySwitch(string id, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (id == null || !_definitions.TryGetValue(id, out var definition))
                return UniTask.FromResult(false);
            PendingSwitchId = null;
            Current = Resolve(definition);
            Changed?.Invoke(Current);
            return UniTask.FromResult(true);
        }

        public string GreetingFor(string id) =>
            id != null && _definitions.TryGetValue(id, out var d) ? Text(d.GreetingKey) : null;

        public void Dispose() => _localizer.Changed -= OnLocaleChanged;

        void OnLocaleChanged()
        {
            Current = Resolve(_definitions[Current.Id]);
            Changed?.Invoke(Current);
        }

        PersonaProfile Resolve(PersonaDefinition d) => new()
        {
            Id = d.Id,
            DisplayName = Text(d.DisplayNameKey) ?? d.Id,
            SystemPrompt = Text(d.SystemPromptKey) ?? string.Empty,
            SafetyNotice = Text(d.SafetyNoticeKey),
            Voice = VoiceFor(d),
            AllowedTools = d.AllowedTools,
            PersistMemory = d.PersistMemory,
            DefaultMood = d.DefaultMood,
        };

        VoiceSpec VoiceFor(PersonaDefinition d)
        {
            if (d.Voices != null && d.Voices.TryGetValue(_localizer.LanguageTag, out var voice))
                return voice;
            return _localizer.Voice?.TtsVoice ?? new VoiceSpec();
        }

        /// <summary>Null for a null key or a missing entry (ILocalizer returns the key itself when missing).</summary>
        string Text(string key)
        {
            if (key == null)
                return null;
            var value = _localizer.Get(Table, key);
            return value == key ? null : value;
        }
    }
}
