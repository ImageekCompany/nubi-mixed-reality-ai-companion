using System;
using System.Collections.Generic;
using System.Text;

namespace Jolly.App.SelfTest
{
    /// <summary>Word overlap of two transcripts, 0..1 (Dice coefficient over lower-case words, punctuation ignored).</summary>
    public static class TextSimilarity
    {
        public static double Words(string a, string b)
        {
            var left = Tokens(a);
            var right = Tokens(b);
            if (left.Count == 0 || right.Count == 0)
                return 0;
            var remaining = new List<string>(right);
            var shared = 0;
            foreach (var word in left)
            {
                if (remaining.Remove(word))
                    shared++;
            }
            return 2.0 * shared / (left.Count + right.Count);
        }

        static List<string> Tokens(string text)
        {
            var words = new List<string>();
            if (string.IsNullOrEmpty(text))
                return words;
            var word = new StringBuilder();
            foreach (var c in text)
            {
                if (char.IsLetterOrDigit(c) || c == '\'')
                {
                    word.Append(char.ToLowerInvariant(c));
                    continue;
                }
                Flush(word, words);
            }
            Flush(word, words);
            return words;
        }

        static void Flush(StringBuilder word, List<string> words)
        {
            if (word.Length > 0)
                words.Add(word.ToString());
            word.Clear();
        }
    }
}
