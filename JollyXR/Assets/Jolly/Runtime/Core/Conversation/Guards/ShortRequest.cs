using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A turn that asks for one thing: one clause of at most <see cref="ActionGuard.MaxWords"/> words with no connector
    /// word (<see cref="GuardTables.ActionConnectors"/>). Only such a turn is answered by a move alone (ADR-0047).
    /// </summary>
    sealed class ShortRequest
    {
        readonly LocalizedPhrases _connectors;
        readonly List<string> _tokens = new();
        readonly List<int> _clauses = new();

        public ShortRequest(ILocalizer localizer) =>
            _connectors = new LocalizedPhrases(localizer, GuardTables.ActionConnectors);

        public bool Matches(string text)
        {
            TextNormalizer.Tokenize(text, _tokens, _clauses);
            if (_tokens.Count == 0 || _tokens.Count > ActionGuard.MaxWords || _clauses[0] != _clauses[_clauses.Count - 1])
                return false;
            foreach (var connector in _connectors.Current)
            {
                if (TextNormalizer.IndexOf(_tokens, connector) >= 0)
                    return false;
            }
            return true;
        }
    }
}
