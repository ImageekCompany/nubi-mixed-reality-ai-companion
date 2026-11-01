using System.Collections.Generic;
using System.Text;

namespace Jolly.Core.Conversation
{
    /// <summary>Lower-case word tokens of a transcript; every non letter/digit is a separator ("don't" → "don", "t").</summary>
    static class TextNormalizer
    {
        public static void Tokenize(string text, List<string> into) => Tokenize(text, into, null);

        /// <summary>Also records, per token, the index of its clause: clauses end at , . ! ? ; and :.</summary>
        public static void Tokenize(string text, List<string> into, List<int> clauses)
        {
            into.Clear();
            clauses?.Clear();
            if (string.IsNullOrEmpty(text))
                return;
            var word = new StringBuilder();
            var clause = 0;
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                    continue;
                }
                if (word.Length > 0)
                {
                    into.Add(word.ToString());
                    clauses?.Add(clause);
                    word.Clear();
                }
                if (c is ',' or '.' or '!' or '?' or ';' or ':')
                    clause++;
            }
            if (word.Length > 0)
            {
                into.Add(word.ToString());
                clauses?.Add(clause);
            }
        }

        public static string[] Tokenize(string text)
        {
            var tokens = new List<string>();
            Tokenize(text, tokens);
            return tokens.ToArray();
        }

        /// <summary>Index of <paramref name="phrase"/> as a whole-token run inside <paramref name="tokens"/>, or -1.</summary>
        public static int IndexOf(IReadOnlyList<string> tokens, string[] phrase, int start = 0)
        {
            for (var i = start; i + phrase.Length <= tokens.Count; i++)
            {
                var match = true;
                for (var j = 0; j < phrase.Length && match; j++)
                    match = tokens[i + j] == phrase[j];
                if (match)
                    return i;
            }
            return -1;
        }
    }
}
