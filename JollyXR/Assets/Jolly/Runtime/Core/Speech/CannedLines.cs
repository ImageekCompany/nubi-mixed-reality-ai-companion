using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Personas;

namespace Jolly.Core.Speech
{
    /// <summary>
    /// Picks a random line of a category, never one of the category's last five picks (when the category has more
    /// lines than that; otherwise everything but the most recent ones). Text comes from the <see cref="Table"/> string
    /// table, falling back to the English catalog text, with the character's name filled in; the clip comes from
    /// <see cref="ILineClipSource"/>.
    /// </summary>
    public sealed class CannedLines : ICannedLines
    {
        public const string Table = "Lines";
        const int HistoryLength = 5;

        readonly Dictionary<LineCategory, List<CannedLineEntry>> _byCategory = new();
        readonly Dictionary<LineCategory, List<CannedLineEntry>> _recent = new();
        readonly List<CannedLineEntry> _candidates = new();
        readonly CharacterNamedLocalizer _localizer;
        readonly ILineClipSource _clips;
        readonly IRandom _random;

        public CannedLines(IReadOnlyList<CannedLineEntry> catalog, CharacterNamedLocalizer localizer, ILineClipSource clips,
                           IRandom random)
        {
            _localizer = localizer;
            _clips = clips;
            _random = random;
            foreach (var entry in catalog)
            {
                if (!_byCategory.TryGetValue(entry.Category, out var entries))
                {
                    entries = new List<CannedLineEntry>();
                    _byCategory.Add(entry.Category, entries);
                    _recent.Add(entry.Category, new List<CannedLineEntry>(HistoryLength));
                }
                entries.Add(entry);
            }
        }

        public bool TryPick(LineCategory category, out CannedLine line)
        {
            if (!_byCategory.TryGetValue(category, out var entries))
            {
                line = null;
                return false;
            }

            var recent = _recent[category];
            var avoid = System.Math.Min(recent.Count, entries.Count - 1);
            _candidates.Clear();
            foreach (var entry in entries)
            {
                if (!IsAmongLast(recent, entry, avoid))
                    _candidates.Add(entry);
            }

            var picked = _candidates[_random.Range(0, _candidates.Count)];
            Remember(recent, picked);
            line = Resolve(picked);
            return true;
        }

        CannedLine Resolve(CannedLineEntry entry)
        {
            var text = _localizer.Get(Table, entry.Key);
            if (string.IsNullOrEmpty(text) || text == entry.Key)
                text = _localizer.Fill(entry.Text);
            _clips.TryGet(_localizer.LanguageTag, entry.Key, out var clip);
            return new CannedLine(entry.Key, text, clip);
        }

        static bool IsAmongLast(List<CannedLineEntry> recent, CannedLineEntry entry, int count)
        {
            for (var i = recent.Count - count; i < recent.Count; i++)
            {
                if (recent[i] == entry)
                    return true;
            }
            return false;
        }

        static void Remember(List<CannedLineEntry> recent, CannedLineEntry entry)
        {
            if (recent.Count == HistoryLength)
                recent.RemoveAt(0);
            recent.Add(entry);
        }
    }
}
