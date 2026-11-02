using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A turn that asks for one thing: one clause of at most <see cref="ActionGuard.MaxWords"/> words with no connector
    /// word (<see cref="GuardTables.ActionConnectors"/>) after its first. A clause of only fillers ("Jolly, do a dance",
    /// "Hey, can you jump?") does not count: it cost those turns 1.5-2 s of closing round. Only such a turn is answered by
    /// a move alone (ADR-0047). Without punctuation or a connector ("Dance tell me a joke") the rest of a request is
    /// indistinguishable, and is lost.
    /// </summary>
    sealed class ShortRequest
    {
        readonly LocalizedPhrases _connectors;
        readonly LocalizedPhrases _fillers;
        readonly List<string> _said = new();
        readonly List<string> _tokens = new();
        readonly List<int> _clauses = new();

        public ShortRequest(ILocalizer localizer)
        {
            _connectors = new LocalizedPhrases(localizer, GuardTables.ActionConnectors);
            _fillers = new LocalizedPhrases(localizer, GuardTables.ActionFillers);
        }

        public bool Matches(string text)
        {
            TextNormalizer.Tokenize(text, _tokens, _clauses);
            _said.Clear();
            var clause = -1;
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (IsFillerClause(_clauses[i]))
                    continue;
                if (clause >= 0 && _clauses[i] != clause)
                    return false;
                clause = _clauses[i];
                _said.Add(_tokens[i]);
            }
            if (_tokens.Count == 0 || _said.Count > ActionGuard.MaxWords)
                return false;
            foreach (var connector in _connectors.Current)
            {
                if (TextNormalizer.IndexOf(_said, connector, 1) >= 0)
                    return false;
            }
            return true;
        }

        bool IsFillerClause(int clause)
        {
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (_clauses[i] == clause && !IsFiller(_tokens[i]))
                    return false;
            }
            return true;
        }

        bool IsFiller(string token)
        {
            foreach (var filler in _fillers.Current)
            {
                if (filler.Length == 1 && filler[0] == token)
                    return true;
            }
            return false;
        }
    }
}
