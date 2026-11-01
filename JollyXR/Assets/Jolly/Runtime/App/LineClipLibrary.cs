using System;
using Jolly.Character;
using Jolly.Core.Speech;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// The baked canned-line clips per language (ADR-0018: a plain asset instead of the <c>LineClips</c> Addressables
    /// table). The clips are small and must play with no network, so they ship with the scene. Lines that say the
    /// character's name are not here: each character holds its own (<see cref="CharacterDefinition.lineClips"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Jolly/Line Clip Library", fileName = "LineClips")]
    public sealed class LineClipLibrary : ScriptableObject, ILineClipSource
    {
        [Tooltip("Each clip is named after its line key, e.g. greeting_02.")]
        public LineClipSet[] languages = Array.Empty<LineClipSet>();

        LineClipIndex _index;

        public bool TryGet(string languageTag, string key, out AudioClip clip)
        {
            _index ??= new LineClipIndex(languages);
            return _index.TryGet(languageTag, key, out clip);
        }

        void OnValidate() => _index = null;
    }
}
