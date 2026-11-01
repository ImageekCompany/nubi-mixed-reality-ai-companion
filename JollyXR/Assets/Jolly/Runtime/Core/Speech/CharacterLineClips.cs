using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jolly.Core.Speech
{
    /// <summary>
    /// The clip for a canned line as the selected character says it. A line that names the character
    /// (<see cref="CannedLineEntry.SaysName"/>) plays only the character's own clip, because a shared one would say
    /// another character's name; without one it is shown as a caption only. Every other line prefers the character's
    /// clip and falls back to the shared library.
    /// </summary>
    public sealed class CharacterLineClips : ILineClipSource
    {
        readonly ILineClipSource _character;
        readonly ILineClipSource _shared;
        readonly HashSet<string> _namedKeys = new(StringComparer.Ordinal);

        public CharacterLineClips(ILineClipSource character, ILineClipSource shared, IEnumerable<CannedLineEntry> catalog)
        {
            _character = character;
            _shared = shared;
            foreach (var entry in catalog)
            {
                if (entry.SaysName)
                    _namedKeys.Add(entry.Key);
            }
        }

        public bool TryGet(string languageTag, string key, out AudioClip clip)
        {
            clip = null;
            if (_character != null && _character.TryGet(languageTag, key, out clip))
                return true;
            return !_namedKeys.Contains(key) && _shared != null && _shared.TryGet(languageTag, key, out clip);
        }
    }
}
