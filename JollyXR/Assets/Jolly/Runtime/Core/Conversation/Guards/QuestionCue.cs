using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// A final that asks something, the only kind that gets the acknowledgement (ADR-0040): a polite request opener
    /// ("can you", "could you") never does, then a '?' anywhere or a question opener ("what", "is", "do") as the first
    /// words after leading fillers ("Nubi", "hey") does. The opener list covers finals without punctuation.
    /// </summary>
    public sealed class QuestionCue
    {
        readonly LocalizedPhrases _openers;
        readonly LocalizedPhrases _requests;
        readonly LocalizedPhrases _fillers;
        readonly List<string> _tokens = new();

        public QuestionCue(ILocalizer localizer)
        {
            _openers = new LocalizedPhrases(localizer, GuardTables.QuestionOpeners);
            _requests = new LocalizedPhrases(localizer, GuardTables.QuestionRequests);
            _fillers = new LocalizedPhrases(localizer, GuardTables.QuestionFillers);
        }

        public bool Matches(string transcript)
        {
            TextNormalizer.Tokenize(transcript, _tokens);
            if (_tokens.Count == 0)
                return false;
            var start = 0;
            while (StartsWithAny(_fillers.Current, start, out var length))
                start += length;
            if (StartsWithAny(_requests.Current, start, out _))
                return false;
            return transcript.IndexOf('?') >= 0 || StartsWithAny(_openers.Current, start, out _);
        }

        bool StartsWithAny(IReadOnlyList<string[]> phrases, int at, out int length)
        {
            foreach (var phrase in phrases)
            {
                if (at + phrase.Length <= _tokens.Count && TextNormalizer.IndexOf(_tokens, phrase, at) == at)
                {
                    length = phrase.Length;
                    return true;
                }
            }
            length = 0;
            return false;
        }
    }
}
