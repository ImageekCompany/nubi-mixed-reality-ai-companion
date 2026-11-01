using System;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Localization
{
    /// <summary>
    /// Locale code → ASR bias, whether Wit can speak it, and reply language (architecture §10, ADR-0014). The voice itself
    /// belongs to the character (<c>VoiceProfile</c>, ADR-0031).
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Locale Voice Map", fileName = "LocaleVoiceMap")]
    public sealed class LocaleVoiceMapAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string languageTag = "en";
            public string asrLanguageBias = "English";
            public string replyLanguageName = "English";
            [Tooltip("False: captions and baked lines only (no Wit voice for this locale).")]
            public bool ttsAvailable = true;
        }

        [SerializeField] Entry[] _entries = { new() };
        [Tooltip("Used when a locale has no entry.")]
        [SerializeField] string _fallbackLanguageTag = "en";

        /// <summary>A fresh profile per call, so callers cannot mutate the asset's data. Falls back to the fallback tag,
        /// then to the first entry.</summary>
        public LocaleVoiceProfile Resolve(string languageTag)
        {
            var entry = Find(languageTag) ?? Find(_fallbackLanguageTag) ?? (_entries.Length > 0 ? _entries[0] : new Entry());
            return new LocaleVoiceProfile
            {
                LanguageTag = entry.languageTag,
                AsrLanguageBias = entry.asrLanguageBias,
                ReplyLanguageName = entry.replyLanguageName,
                TtsAvailable = entry.ttsAvailable,
            };
        }

        Entry Find(string languageTag)
        {
            foreach (var e in _entries)
            {
                if (string.Equals(e.languageTag, languageTag, StringComparison.OrdinalIgnoreCase))
                    return e;
            }
            return null;
        }
    }
}
