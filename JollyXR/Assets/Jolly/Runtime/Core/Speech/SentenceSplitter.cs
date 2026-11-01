using System.Text;
using Jolly.Core.Conversation;

namespace Jolly.Core.Speech
{
    /// <summary>
    /// Cuts streamed model text into TTS-sized sentences (architecture §5.1): on <c>.?!…</c> followed by whitespace,
    /// on a newline, at the last comma or space once the buffer reaches 120 characters (60 and a comma for the first
    /// chunk, to start audio sooner), and never longer than <see cref="MaxLength"/>. Call <see cref="TryTake"/> until it
    /// returns false before <see cref="Flush"/>.
    /// </summary>
    public sealed class SentenceSplitter : ISentenceSplitter
    {
        public const int MaxLength = 240;
        const int FirstChunkLength = 60;
        const int ChunkLength = 120;

        static readonly string[] Abbreviations =
            { "mr", "mrs", "ms", "dr", "prof", "sr", "jr", "st", "vs", "e.g", "i.e", "approx", "mt", "fig" };

        readonly StringBuilder _buffer = new(MaxLength * 2);
        bool _isFirstChunk = true;

        public void Append(string delta)
        {
            if (string.IsNullOrEmpty(delta))
                return;
            foreach (var c in delta)
            {
                if (c == '。')
                    _buffer.Append(". ");
                else
                    _buffer.Append(c);
            }
        }

        public bool TryTake(out string sentence)
        {
            while (true)
            {
                var end = FindSentenceEnd();
                if (end > MaxLength)
                    end = -1;
                if (end < 0)
                    end = FindLengthSplit();
                if (end < 0)
                {
                    sentence = null;
                    return false;
                }

                sentence = Take(end);
                if (sentence.Length > 0)
                {
                    _isFirstChunk = false;
                    return true;
                }
            }
        }

        /// <summary>Returns whatever is left (trimmed, possibly empty) and starts a new utterance.</summary>
        public string Flush()
        {
            var rest = Take(_buffer.Length);
            _isFirstChunk = true;
            return rest;
        }

        int FindSentenceEnd()
        {
            var length = _buffer.Length;
            for (var i = 0; i < length; i++)
            {
                var c = _buffer[i];
                if (c == '\n')
                    return i + 1;
                if (!IsTerminator(c))
                    continue;

                var next = i + 1;
                while (next < length && IsCloser(_buffer[next]))
                    next++;
                if (next == length)
                    return -1;
                if (char.IsWhiteSpace(_buffer[next]) && !IsAbbreviation(i))
                    return next;
            }
            return -1;
        }

        int FindLengthSplit()
        {
            var length = _buffer.Length;
            var threshold = _isFirstChunk ? FirstChunkLength : ChunkLength;
            if (length < threshold)
                return -1;

            var window = length < MaxLength ? length : MaxLength;
            for (var i = window - 1; i > 0; i--)
            {
                if (_buffer[i] == ',')
                    return i + 1;
            }
            if (length < ChunkLength)
                return -1;
            for (var i = window - 1; i > 0; i--)
            {
                if (char.IsWhiteSpace(_buffer[i]))
                    return i + 1;
            }
            return length >= MaxLength ? MaxLength : -1;
        }

        bool IsAbbreviation(int periodIndex)
        {
            if (_buffer[periodIndex] != '.')
                return false;

            var start = periodIndex;
            while (start > 0 && !char.IsWhiteSpace(_buffer[start - 1]) && !IsOpener(_buffer[start - 1]))
                start--;
            var tokenLength = periodIndex - start;
            if (tokenLength == 1)
                return char.IsUpper(_buffer[start]);

            foreach (var abbreviation in Abbreviations)
            {
                if (abbreviation.Length == tokenLength && TokenEquals(start, abbreviation))
                    return true;
            }
            return false;
        }

        bool TokenEquals(int start, string lowerCase)
        {
            for (var i = 0; i < lowerCase.Length; i++)
            {
                if (char.ToLowerInvariant(_buffer[start + i]) != lowerCase[i])
                    return false;
            }
            return true;
        }

        string Take(int count)
        {
            var text = _buffer.ToString(0, count).Trim();
            _buffer.Remove(0, count);
            return text;
        }

        static bool IsTerminator(char c) => c is '.' or '?' or '!' or '…';
        static bool IsCloser(char c) => c is '"' or '\'' or ')' or ']' or '”' or '’';
        static bool IsOpener(char c) => c is '"' or '\'' or '(' or '[' or '“' or '‘';
    }
}
