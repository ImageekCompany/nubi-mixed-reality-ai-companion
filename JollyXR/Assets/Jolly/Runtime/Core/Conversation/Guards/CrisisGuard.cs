using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Deterministic crisis hand-off (architecture §9): matches a localized keyword phrase anywhere in the final
    /// transcript, before any model call, so the hand-off never depends on the model.
    /// </summary>
    public sealed class CrisisGuard
    {
        readonly LocalizedPhrases _keywords;
        readonly List<string> _tokens = new();

        public CrisisGuard(ILocalizer localizer) => _keywords = new LocalizedPhrases(localizer, GuardTables.CrisisKeywords);

        public bool Matches(string transcript)
        {
            TextNormalizer.Tokenize(transcript, _tokens);
            foreach (var phrase in _keywords.Current)
            {
                if (TextNormalizer.IndexOf(_tokens, phrase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
