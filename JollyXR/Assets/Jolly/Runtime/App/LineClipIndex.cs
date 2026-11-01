using System;
using System.Collections.Generic;
using Jolly.Character;
using Jolly.Core.Speech;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>Clips by language tag (any case) and clip name.</summary>
    public sealed class LineClipIndex : ILineClipSource
    {
        readonly Dictionary<string, Dictionary<string, AudioClip>> _byLanguage = new(StringComparer.OrdinalIgnoreCase);

        public LineClipIndex(IEnumerable<LineClipSet> sets)
        {
            foreach (var set in sets ?? Array.Empty<LineClipSet>())
            {
                if (set == null)
                    continue;
                var clips = set.clips ?? Array.Empty<AudioClip>();
                var byKey = new Dictionary<string, AudioClip>(clips.Length, StringComparer.Ordinal);
                foreach (var clip in clips)
                {
                    if (clip != null)
                        byKey[clip.name] = clip;
                }
                _byLanguage[set.languageTag] = byKey;
            }
        }

        public bool TryGet(string languageTag, string key, out AudioClip clip)
        {
            clip = null;
            return languageTag != null && _byLanguage.TryGetValue(languageTag, out var byKey) && byKey.TryGetValue(key, out clip);
        }
    }
}
