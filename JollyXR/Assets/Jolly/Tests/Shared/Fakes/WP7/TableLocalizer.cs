using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>ILocalizer over in-memory "table/key" entries per language; a missing entry returns the key, as the real one does.</summary>
    public sealed class TableLocalizer : ILocalizer
    {
        readonly Dictionary<string, string> _entries = new();
        readonly List<string> _languages = new() { "en" };

        public string LanguageTag { get; private set; } = "en";
        public LocaleVoiceProfile Voice { get; set; } = new() { LanguageTag = "en", TtsAvailable = true };
        public IReadOnlyList<string> AvailableLanguageTags => _languages;

        public event Action Changed;

        public TableLocalizer With(string languageTag, string table, string key, string value)
        {
            _entries[Compose(languageTag, table, key)] = value;
            if (!_languages.Contains(languageTag))
                _languages.Add(languageTag);
            return this;
        }

        public string Get(string table, string key)
            => _entries.TryGetValue(Compose(LanguageTag, table, key), out var value) ? value : key;

        public UniTask SetLanguage(string languageTag, CancellationToken ct)
        {
            LanguageTag = languageTag;
            Changed?.Invoke();
            return UniTask.CompletedTask;
        }

        static string Compose(string languageTag, string table, string key) => languageTag + "/" + table + "/" + key;
    }
}
