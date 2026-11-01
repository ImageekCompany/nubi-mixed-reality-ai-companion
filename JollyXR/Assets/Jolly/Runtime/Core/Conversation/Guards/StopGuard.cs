using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Local "stop" (architecture §5.1): a short final made only of stop triggers ("stop", "wait", "hold on") and
    /// fillers ("Nubi", "please"). The pipeline halts locomotion on a match before the model is called.
    /// </summary>
    public sealed class StopGuard
    {
        const int MaxWords = 4;

        readonly LocalizedPhrases _triggers;
        readonly LocalizedPhrases _fillers;
        readonly List<string> _tokens = new();
        readonly bool[] _covered = new bool[MaxWords];

        public StopGuard(ILocalizer localizer)
        {
            _triggers = new LocalizedPhrases(localizer, GuardTables.StopTriggers);
            _fillers = new LocalizedPhrases(localizer, GuardTables.StopFillers);
        }

        public bool Matches(string transcript)
        {
            TextNormalizer.Tokenize(transcript, _tokens);
            if (_tokens.Count == 0 || _tokens.Count > MaxWords)
                return false;

            System.Array.Clear(_covered, 0, _covered.Length);
            var triggered = Cover(_triggers.Current);
            Cover(_fillers.Current);
            for (var i = 0; i < _tokens.Count; i++)
            {
                if (!_covered[i])
                    return false;
            }
            return triggered;
        }

        bool Cover(IReadOnlyList<string[]> phrases)
        {
            var any = false;
            foreach (var phrase in phrases)
            {
                for (var at = TextNormalizer.IndexOf(_tokens, phrase); at >= 0; at = TextNormalizer.IndexOf(_tokens, phrase, at + 1))
                {
                    for (var j = 0; j < phrase.Length; j++)
                        _covered[at + j] = true;
                    any = true;
                }
            }
            return any;
        }
    }
}
