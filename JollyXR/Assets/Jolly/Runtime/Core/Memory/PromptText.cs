using System;
using System.Globalization;
using System.Text;

namespace Jolly.Core.Memory
{
    /// <summary>
    /// Sanitises text that reaches the prompt's context block from camera-derived sources (architecture §7): printable,
    /// one line, no quotes, brackets or braces, collapsed whitespace, truncated.
    /// </summary>
    public static class PromptText
    {
        public static string Sanitize(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            var sb = new StringBuilder(Math.Min(text.Length, maxChars));
            var space = false;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (IsStripped(c))
                    continue;
                if (sb.Length + (space ? 2 : 1) > maxChars)
                    break;
                if (space)
                    sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        static bool IsStripped(char c) =>
            c is '"' or '\'' or '`' or '<' or '>' or '{' or '}' or '[' or ']' || char.GetUnicodeCategory(c) is
                UnicodeCategory.InitialQuotePunctuation or
                UnicodeCategory.FinalQuotePunctuation or
                UnicodeCategory.Format or
                UnicodeCategory.Surrogate or
                UnicodeCategory.PrivateUse or
                UnicodeCategory.OtherNotAssigned;
    }
}
