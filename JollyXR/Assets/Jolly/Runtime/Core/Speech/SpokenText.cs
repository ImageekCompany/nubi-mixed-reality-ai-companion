using System;
using System.Text.RegularExpressions;

namespace Jolly.Core.Speech
{
    /// <summary>
    /// Removes what must not be read aloud or captioned from one model sentence: markdown (emphasis, headings, list
    /// markers, code ticks, tables), links (the label stays), bare URLs, source attributions such as "(bbc.co.uk)" and
    /// citation markers such as "[1]" or "【3†source】". Web-search answers carry these despite the prompt (ADR-0048).
    /// </summary>
    public static class SpokenText
    {
        static readonly char[] Markers = { '*', '_', '#', '`', '[', ']', '(', '|', '>', '【', '~', '/', '-', '+', '•' };

        static readonly Regex Link = new(@"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex Citation = new(@"\[\d+(?:\s*,\s*\d+)*\]|【[^】]*】", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex Url = new(@"\(?\b(?:https?://|www\.)\S+?\)?(?=[\s.,;:!?]*(?:\s|$))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        static readonly Regex DomainSource = new(@"\(\s*(?:[a-z0-9-]+\.)+[a-z]{2,}(?:/\S*)?\s*\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        static readonly Regex LinePrefix = new(@"^\s*(?:#{1,6}\s+|>\s*|[-*+•]\s+|\d{1,2}[.)]\s+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex ListNumberOnly = new(@"^\s*\d{1,2}[.)]\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex Emphasis = new(@"(\*{1,3}|_{2,3}|~~|`+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex TableRule = new(@"^\s*\|?[\s:|-]*-{3,}[\s:|-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex Space = new(@"\s{2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex SpaceBeforePunctuation = new(@"\s+([.,;:!?])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>The sentence unchanged when nothing in it can be markup (most); empty when nothing speakable is left.</summary>
        public static string Clean(string sentence)
        {
            if (string.IsNullOrEmpty(sentence) || !MayHoldMarkup(sentence))
                return sentence;
            if (ListNumberOnly.IsMatch(sentence) || TableRule.IsMatch(sentence))
                return string.Empty;
            var text = Link.Replace(sentence, "$1");
            text = Citation.Replace(text, string.Empty);
            text = Url.Replace(text, string.Empty);
            text = DomainSource.Replace(text, string.Empty);
            text = LinePrefix.Replace(text, string.Empty);
            text = Emphasis.Replace(text, string.Empty);
            text = text.Replace('|', ',');
            text = text.Replace("()", string.Empty);
            text = SpaceBeforePunctuation.Replace(text, "$1");
            return Space.Replace(text, " ").Trim().Trim(',').Trim();
        }

        /// <summary>A marker character, a "www." or a leading digit (a numbered list item).</summary>
        static bool MayHoldMarkup(string sentence)
        {
            if (sentence.IndexOfAny(Markers) >= 0 || sentence.IndexOf("www.", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (var c in sentence)
            {
                if (!char.IsWhiteSpace(c))
                    return char.IsDigit(c);
            }
            return false;
        }
    }
}
