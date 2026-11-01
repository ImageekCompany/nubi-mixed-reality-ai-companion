using System.Text;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Punctuation clean-up for transcripts, as the TranscriptEvent contract promises. Seen live: a 16 kHz final ending in
    /// the ideographic "。" and an SSE final ending in "is.." (meta-model-api.md §5). A run of exactly two full stops
    /// collapses to one; an ellipsis is kept.
    /// </summary>
    public static class TranscriptText
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (!NeedsWork(text))
                return text;

            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                var c = Map(text[i]);
                var doubleStop = c == '.' && i + 1 < text.Length && Map(text[i + 1]) == '.'
                                 && (i + 2 >= text.Length || Map(text[i + 2]) != '.')
                                 && (i == 0 || Map(text[i - 1]) != '.');
                sb.Append(c);
                if (doubleStop)
                    i++;
            }
            return sb.ToString().Trim();
        }

        static bool NeedsWork(string text)
        {
            if (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1]))
                return true;
            for (var i = 0; i < text.Length; i++)
            {
                if (Map(text[i]) != text[i] || (text[i] == '.' && i + 1 < text.Length && text[i + 1] == '.'))
                    return true;
            }
            return false;
        }

        static char Map(char c) => c switch
        {
            '。' => '.',
            '？' => '?',
            '！' => '!',
            '，' => ',',
            _ => c,
        };
    }
}
