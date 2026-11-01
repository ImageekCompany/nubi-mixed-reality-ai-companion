using System.Collections.Generic;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Offline
{
    /// <summary>
    /// ICannedLines over hand-added lines. Honours the contract's "avoid the last 5 picks of the category" by cycling
    /// through each category in insertion order, so picks are deterministic.
    /// </summary>
    public sealed class FakeCannedLines : ICannedLines
    {
        readonly Dictionary<LineCategory, List<CannedLine>> _lines = new();
        readonly Dictionary<LineCategory, int> _next = new();
        readonly List<LineCategory> _requested = new();

        public IReadOnlyList<LineCategory> Requested => _requested;

        public FakeCannedLines Add(LineCategory category, string key, AudioClip clip = null)
        {
            if (!_lines.TryGetValue(category, out var list))
                _lines[category] = list = new List<CannedLine>();
            list.Add(new CannedLine(key, "text:" + key, clip));
            return this;
        }

        public bool TryPick(LineCategory category, out CannedLine line)
        {
            _requested.Add(category);
            if (!_lines.TryGetValue(category, out var list) || list.Count == 0)
            {
                line = null;
                return false;
            }
            _next.TryGetValue(category, out var index);
            line = list[index % list.Count];
            _next[category] = index + 1;
            return true;
        }
    }
}
