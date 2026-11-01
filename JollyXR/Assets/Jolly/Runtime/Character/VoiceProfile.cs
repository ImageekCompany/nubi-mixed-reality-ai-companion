using System;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// A character's Wit voice as data (ADR-0031): one default plus optional per-language replacements. Personas only
    /// scale it (<c>VoiceDelivery</c>), and the canned-line baker reads the same asset, so live and offline speech match.
    /// Wit has no gain parameter (it is ignored), so there is none here; effects are SSML <c>&lt;sfx&gt;</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Voice Profile", fileName = "Voice")]
    public sealed class VoiceProfile : ScriptableObject, ICharacterVoice
    {
        [Serializable]
        public sealed class Voice
        {
            [Tooltip("Wit voice name from GET /voices without the 'wit$' prefix, e.g. Rubie.")]
            public string voiceId = "Charlie";
            [Tooltip("Empty = the voice's default style. Wit lists default, soft, formal, fast, projected.")]
            public string style = "";
            [Range(VoiceSpec.MinSpeed, VoiceSpec.MaxSpeed)] public int speed = 100;
            [Range(VoiceSpec.MinPitch, VoiceSpec.MaxPitch)] public int pitch = 100;
            [Tooltip("SSML sfx character: chipmunk, monster, daemon, robot, alien. Empty = none.")]
            public string sfxCharacter = "";
            [Tooltip("SSML sfx environment: reverb, room, cathedral, radio, phone, pa, helmet. Empty = none.")]
            public string sfxEnvironment = "";

            public VoiceSpec ToSpec() => new()
            {
                VoiceId = voiceId, Style = NullIfEmpty(style), Speed = speed, Pitch = pitch,
                SfxCharacter = NullIfEmpty(sfxCharacter), SfxEnvironment = NullIfEmpty(sfxEnvironment),
            };

            static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
        }

        [Serializable]
        public sealed class LocaleVoice
        {
            [Tooltip("Language tag, e.g. pl. Also matches regional tags: pl matches pl-PL.")]
            public string languageTag = "";
            public Voice voice = new();
        }

        [Tooltip("Spoken in every language without its own entry.")]
        public Voice defaultVoice = new();
        [Tooltip("Whether a locale can speak at all is LocaleVoiceMap.ttsAvailable; this only picks the voice.")]
        public LocaleVoice[] locales = Array.Empty<LocaleVoice>();

        public VoiceSpec VoiceFor(string languageTag) => (Find(languageTag) ?? defaultVoice).ToSpec();

        Voice Find(string languageTag)
        {
            if (string.IsNullOrEmpty(languageTag))
                return null;
            Voice primary = null;
            foreach (var l in locales)
            {
                if (string.Equals(l.languageTag, languageTag, StringComparison.OrdinalIgnoreCase))
                    return l.voice;
                if (primary == null && IsPrimaryOf(l.languageTag, languageTag))
                    primary = l.voice;
            }
            return primary;
        }

        static bool IsPrimaryOf(string primary, string tag) =>
            !string.IsNullOrEmpty(primary) && tag.Length > primary.Length
            && tag.StartsWith(primary, StringComparison.OrdinalIgnoreCase) && (tag[primary.Length] == '-' || tag[primary.Length] == '_');
    }
}
