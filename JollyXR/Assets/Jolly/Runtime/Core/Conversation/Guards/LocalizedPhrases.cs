using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>A '|'-separated phrase list from the <see cref="GuardTables"/> table, tokenised once per locale.</summary>
    sealed class LocalizedPhrases
    {
        readonly ILocalizer _localizer;
        readonly string _key;
        string _languageTag;
        List<string[]> _phrases;

        public LocalizedPhrases(ILocalizer localizer, string key)
        {
            _localizer = localizer;
            _key = key;
        }

        public IReadOnlyList<string[]> Current
        {
            get
            {
                if (_phrases == null || _languageTag != _localizer.LanguageTag)
                    Load();
                return _phrases;
            }
        }

        void Load()
        {
            _languageTag = _localizer.LanguageTag;
            var raw = _localizer.Get(GuardTables.Table, _key);
            if (string.IsNullOrWhiteSpace(raw) || raw == _key)
                raw = GuardTables.Fallback(_key, _languageTag);

            _phrases = new List<string[]>();
            foreach (var phrase in raw.Split('|'))
            {
                var tokens = TextNormalizer.Tokenize(phrase);
                if (tokens.Length > 0)
                    _phrases.Add(tokens);
            }
        }
    }
}
