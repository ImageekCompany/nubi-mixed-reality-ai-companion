using System.Collections.Generic;
using System.Text;

namespace Jolly.Core.Conversation
{
    /// <summary>Lower-case word tokens of a transcript; every non letter/digit is a separator ("don't" → "don", "t").</summary>
    static class TextNormalizer
    {
        public static void Tokenize(string text, List<string> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text))
                return;
            var word = new StringBuilder();
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else if (word.Length > 0)
                {
                    into.Add(word.ToString());
                    word.Clear();
                }
            }
            if (word.Length > 0)
                into.Add(word.ToString());
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
