using System;
using System.Collections.Generic;
using System.Text;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Rewrites known ASR mishearings before captions and the model. Whole-word, case-insensitive. Each character lists
    /// the words heard instead of its name (<c>CharacterDefinition.aliases</c>): live ASR heard "Hi Nubi" as
    /// "Hi newbie" and "Nubi, come here" as "Nobi, come here" (2026-11-01).
    /// </summary>
    public sealed class TranscriptAliases
    {
        readonly Dictionary<string, string> _aliases;

        /// <summary>Every alias rewritten to <paramref name="name"/>; blank aliases and the name itself are skipped.</summary>
        public static TranscriptAliases ForName(string name, IEnumerable<string> aliases)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var alias in aliases ?? Array.Empty<string>())
            {
                var heard = alias?.Trim();
                if (!string.IsNullOrEmpty(heard) && !string.Equals(heard, name, StringComparison.OrdinalIgnoreCase))
                    map[heard] = name;
            }
            return new TranscriptAliases(map);
        }

        public TranscriptAliases(IReadOnlyDictionary<string, string> aliases)
        {
            _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in aliases)
                _aliases[pair.Key] = pair.Value;
        }

        public string Apply(string transcript)
        {
            if (string.IsNullOrEmpty(transcript))
                return string.Empty;

            StringBuilder result = null;
            var copied = 0;
            var i = 0;
            while (i < transcript.Length)
            {
                if (!IsWordChar(transcript[i]))
                {
                    i++;
                    continue;
                }
                var start = i;
                while (i < transcript.Length && IsWordChar(transcript[i]))
                    i++;
                if (!_aliases.TryGetValue(transcript.Substring(start, i - start), out var replacement))
                    continue;

                result ??= new StringBuilder(transcript.Length);
                result.Append(transcript, copied, start - copied).Append(replacement);
                copied = i;
            }
            return result == null ? transcript : result.Append(transcript, copied, transcript.Length - copied).ToString();
        }

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'';
    }
}
