using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Testing.Conversation
{
    /// <summary>ILocalizer over a (language, table, key) dictionary. Missing entries return the key, like UnityLocalizer.</summary>
    public sealed class FakeLocalizer : ILocalizer
    {
        readonly Dictionary<(string, string, string), string> _entries = new();
        readonly Dictionary<string, LocaleVoiceProfile> _voices = new();

        public FakeLocalizer(string languageTag = "en")
        {
            LanguageTag = languageTag;
            WithVoice("en", "English", ttsAvailable: true, "English");
            WithVoice("pl", "Polish", ttsAvailable: false, "Polish");
        }

        public string LanguageTag { get; private set; }
        public LocaleVoiceProfile Voice => _voices[LanguageTag];
        public IReadOnlyList<string> AvailableLanguageTags => new List<string>(_voices.Keys);
        public event Action Changed;

        public FakeLocalizer With(string table, string key, string value) => With(LanguageTag, table, key, value);

        public FakeLocalizer With(string languageTag, string table, string key, string value)
        {
            _entries[(languageTag, table, key)] = value;
            return this;
        }

        public FakeLocalizer WithVoice(string languageTag, string asrBias, bool ttsAvailable, string replyLanguage)
        {
            _voices[languageTag] = new LocaleVoiceProfile
            {
                LanguageTag = languageTag, AsrLanguageBias = asrBias, TtsAvailable = ttsAvailable,
                ReplyLanguageName = replyLanguage, TtsVoice = new VoiceSpec { VoiceId = "Charlie" },
            };
            return this;
        }

        public string Get(string table, string key) =>
            _entries.TryGetValue((LanguageTag, table, key), out var value) ? value : key;

        public UniTask SetLanguage(string languageTag, CancellationToken ct)
        {
            LanguageTag = languageTag;
            Changed?.Invoke();
            return UniTask.CompletedTask;
        }
    }
}
