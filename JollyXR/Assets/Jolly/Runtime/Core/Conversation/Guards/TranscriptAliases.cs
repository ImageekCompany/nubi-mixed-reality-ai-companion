using System;
using System.Collections.Generic;
using System.Text;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// Rewrites known ASR mishearings before captions and the model. Whole-word, case-insensitive.
    /// Live ASR heard "Hi Nubi" as "Hi newbie" (2026-11-01).
    /// </summary>
    public sealed class TranscriptAliases
    {
        public static readonly TranscriptAliases Default = new(new Dictionary<string, string>
        {
            ["newbie"] = "Nubi", ["noobie"] = "Nubi", ["nubie"] = "Nubi", ["newby"] = "Nubi",
        });

        readonly Dictionary<string, string> _aliases;

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
