using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A short final made only of trigger phrases and fillers, with at least one trigger: the shape the local guards act
    /// on before the model (<see cref="StopGuard"/>, <see cref="ComeGuard"/>, <see cref="ActionGuard"/>, <see cref="SettingsGuard"/>). Any other
    /// word means the person said more than the command, so the model decides, with one exception unless a guard turns
    /// it off (<c>nameAsHeard</c>): the name as heard. A clause (between , . ! ? ; :) with no trigger and exactly one word
    /// the tables do not know is taken as the person calling the character, whatever ASR made of the name: the
    /// self-test's "Hi Nubi, come here." with Jolly selected came back as "Hi, newbie. newbie. Come here." and went to
    /// the model (ADR-0043).
    /// </summary>
    sealed class ShortCommand
    {
        readonly LocalizedPhrases _triggers;
        readonly LocalizedPhrases _fillers;
        readonly List<string> _tokens = new();
        readonly List<int> _clauses = new();
        readonly bool[] _covered;
        readonly bool[] _trigger;
        readonly bool _nameAsHeard;

        public ShortCommand(ILocalizer localizer, string triggersKey, string fillersKey, int maxWords, bool nameAsHeard = true)
        {
            _nameAsHeard = nameAsHeard;
            _triggers = new LocalizedPhrases(localizer, triggersKey);
            _fillers = new LocalizedPhrases(localizer, fillersKey);
            _covered = new bool[maxWords];
            _trigger = new bool[maxWords];
        }

        public bool Matches(string transcript)
        {
            TextNormalizer.Tokenize(transcript, _tokens, _clauses);
            if (_tokens.Count == 0 || _tokens.Count > _covered.Length)
                return false;

            System.Array.Clear(_covered, 0, _covered.Length);
            var triggered = Cover(_triggers.Current, _trigger);
            Cover(_fillers.Current, null);
            if (_nameAsHeard)
                CoverCalledNames();
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (!_covered[i])
                    return false;
            }
            return triggered;
        }

        /// <summary>Marks covered phrases; <paramref name="byTrigger"/>, when given, is set exactly where a phrase matched.</summary>
        bool Cover(IReadOnlyList<string[]> phrases, bool[] byTrigger)
        {
            if (byTrigger != null)
                System.Array.Clear(byTrigger, 0, byTrigger.Length);
            var any = false;
            foreach (var phrase in phrases)
            {
                for (var at = TextNormalizer.IndexOf(_tokens, phrase); at >= 0; at = TextNormalizer.IndexOf(_tokens, phrase, at + 1))
                {
                    for (var j = 0; j < phrase.Length; j++)
                    {
                        _covered[at + j] = true;
                        if (byTrigger != null)
                            byTrigger[at + j] = true;
                    }
                    any = true;
                }
            }
            return any;
        }

        void CoverCalledNames()
        {
            var start = 0;
            while (start < _tokens.Count)
            {
                var end = start;
                var uncovered = -1;
                var unknown = 0;
                var hasTrigger = false;
                for (; end < _tokens.Count && _clauses[end] == _clauses[start]; end++)
                {
                    hasTrigger |= _trigger[end];
                    if (!_covered[end])
                    {
                        uncovered = end;
                        unknown++;
                    }
                }
                if (unknown == 1 && !hasTrigger)
                    _covered[uncovered] = true;
                start = end;
            }
        }
    }
}
