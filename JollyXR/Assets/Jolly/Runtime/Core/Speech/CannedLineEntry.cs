using Jolly.Contracts;
using Jolly.Core.Personas;
using UnityEngine;

namespace Jolly.Core.Speech
{
    /// <summary>One canned line: its key in the <c>Lines</c> / <c>LineClips</c> tables, category and English source text.</summary>
    public sealed class CannedLineEntry
    {
        public CannedLineEntry(string key, LineCategory category, string text) { Key = key; Category = category; Text = text; }
        public string Key { get; }
        public LineCategory Category { get; }
        public string Text { get; }
        /// <summary>The text names the character, so its baked clip belongs to one character (<see cref="CharacterLineClips"/>).</summary>
        public bool SaysName => CharacterNamedLocalizer.SaysName(Text);
    }

    /// <summary>Baked clip per language and line key (the <c>LineClips</c> asset table at runtime).</summary>
    public interface ILineClipSource
    {
        bool TryGet(string languageTag, string key, out AudioClip clip);
    }
}
