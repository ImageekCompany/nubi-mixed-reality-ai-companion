using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Personas
{
    /// <summary>
    /// Fills <see cref="Token"/> in every string it reads with the selected character's name (ADR-0026), so persona
    /// prompts, greetings, canned lines and stop fillers say the name of the character on screen. The name is fixed
    /// for the session, which keeps each character's prompt-cache prefix stable. It is the <c>nameKey</c> entry of
    /// the <see cref="NameTable"/> table in the current locale when there is one, else the display name.
    /// </summary>
    public sealed class CharacterNamedLocalizer : ILocalizer
    {
        public const string Token = "{name}";
        public const string NameTable = "UI";

        readonly ILocalizer _inner;
        readonly string _displayName;
        readonly string _nameKey;

        public CharacterNamedLocalizer(ILocalizer inner, string displayName, string nameKey = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("a character needs a display name", nameof(displayName));
            _displayName = displayName.Trim();
            _nameKey = string.IsNullOrWhiteSpace(nameKey) ? null : nameKey.Trim();
        }

        public string Name
        {
            get
            {
                if (_nameKey == null)
                    return _displayName;
                var localized = _inner.Get(NameTable, _nameKey);
                return string.IsNullOrWhiteSpace(localized) || localized == _nameKey ? _displayName : localized;
            }
        }

        public string LanguageTag => _inner.LanguageTag;
        public LocaleVoiceProfile Voice => _inner.Voice;
        public IReadOnlyList<string> AvailableLanguageTags => _inner.AvailableLanguageTags;

        public event Action Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        public string Get(string table, string key) => Fill(_inner.Get(table, key));

        /// <summary>Fills text that did not come from a table, such as a built-in fallback.</summary>
        public string Fill(string text) => SaysName(text) ? text.Replace(Token, Name) : text;

        public UniTask SetLanguage(string languageTag, CancellationToken ct) => _inner.SetLanguage(languageTag, ct);

        public static bool SaysName(string text) => text != null && text.Contains(Token);

        public static string Fill(string text, string name) =>
            SaysName(text) ? text.Replace(Token, name) : text;
    }
}
