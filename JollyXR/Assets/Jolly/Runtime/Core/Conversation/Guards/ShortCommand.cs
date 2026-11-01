using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A short final made only of trigger phrases and fillers, with at least one trigger: the shape the local guards act
    /// on before the model (<see cref="StopGuard"/>, <see cref="ComeGuard"/>). Any other word means the person said more
    /// than the command, so the model decides.
    /// </summary>
    sealed class ShortCommand
    {
        readonly LocalizedPhrases _triggers;
        readonly LocalizedPhrases _fillers;
        readonly List<string> _tokens = new();
        readonly bool[] _covered;

        public ShortCommand(ILocalizer localizer, string triggersKey, string fillersKey, int maxWords)
        {
            _triggers = new LocalizedPhrases(localizer, triggersKey);
            _fillers = new LocalizedPhrases(localizer, fillersKey);
            _covered = new bool[maxWords];
        }

        public bool Matches(string transcript)
        {
            TextNormalizer.Tokenize(transcript, _tokens);
            if (_tokens.Count == 0 || _tokens.Count > _covered.Length)
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
