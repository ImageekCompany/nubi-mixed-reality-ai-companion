using System;
using System.Collections.Generic;
using Jolly.Core.Speech;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// The baked canned-line clips per language (ADR-0018: a plain asset instead of the <c>LineClips</c> Addressables
    /// table). The clips are small and must play with no network, so they ship with the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Line Clip Library", fileName = "LineClips")]
    public sealed class LineClipLibrary : ScriptableObject, ILineClipSource
    {
        [Serializable]
        public sealed class Language
        {
            public string languageTag = "en";
            public AudioClip[] clips = Array.Empty<AudioClip>();
        }

        [Tooltip("Each clip is named after its line key, e.g. greeting_01.")]
        public Language[] languages = Array.Empty<Language>();

        Dictionary<string, Dictionary<string, AudioClip>> _index;

        public bool TryGet(string languageTag, string key, out AudioClip clip)
        {
            clip = null;
            _index ??= BuildIndex();
            return languageTag != null && _index.TryGetValue(languageTag, out var byKey) && byKey.TryGetValue(key, out clip);
        }

        Dictionary<string, Dictionary<string, AudioClip>> BuildIndex()
        {
            var index = new Dictionary<string, Dictionary<string, AudioClip>>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in languages)
            {
                var byKey = new Dictionary<string, AudioClip>(language.clips.Length, StringComparer.Ordinal);
                foreach (var clip in language.clips)
                {
                    if (clip != null)
                        byKey[clip.name] = clip;
                }
                index[language.languageTag] = byKey;
            }
            return index;
        }

        void OnValidate() => _index = null;
    }
}
