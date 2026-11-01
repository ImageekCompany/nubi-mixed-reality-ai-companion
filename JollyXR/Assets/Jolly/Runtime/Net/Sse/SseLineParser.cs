using System;
using System.Collections.Generic;
using System.Text;

namespace Jolly.Net.Sse
{
    /// <summary>
    /// Incremental text/event-stream parser (WHATWG HTML §9.2.6) fed with raw bytes as they arrive. A chunk may split
    /// a UTF-8 sequence, a CRLF pair or a line anywhere. Reuses its buffers; only the dispatched strings allocate.
    /// </summary>
    public sealed class SseLineParser
    {
        const int DecodeChunk = 4096;

        readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
        // Not DecodeChunk: bytes the decoder carried over from the previous slice can add a char.
        readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(DecodeChunk)];
        readonly StringBuilder _line = new(256);
        readonly StringBuilder _data = new(1024);
        string _eventName;
        bool _hasData;
        bool _afterCr;

        public void Feed(ReadOnlySpan<byte> bytes, Queue<SseEvent> output)
        {
            while (!bytes.IsEmpty)
            {
                var take = Math.Min(bytes.Length, DecodeChunk);
                var count = _decoder.GetChars(bytes.Slice(0, take), _chars, flush: false);
                for (var i = 0; i < count; i++)
                    Accept(_chars[i], output);
                bytes = bytes.Slice(take);
            }
        }

        /// <summary>End of body: a trailing line or event without its blank line is still delivered.</summary>
        public void Complete(Queue<SseEvent> output)
        {
            if (_line.Length > 0)
                EndLine(output);
            Dispatch(output);
            _afterCr = false;
        }

        void Accept(char c, Queue<SseEvent> output)
        {
            if (c == '\n' && _afterCr)
            {
                _afterCr = false;
                return;
            }
            _afterCr = c == '\r';
            if (c == '\r' || c == '\n')
                EndLine(output);
            else
                _line.Append(c);
        }

        void EndLine(Queue<SseEvent> output)
        {
            if (_line.Length == 0)
                Dispatch(output);
            else if (_line[0] != ':')
                ApplyField();
            _line.Clear();
        }

        void ApplyField()
        {
            var colon = IndexOfColon();
            var nameLength = colon < 0 ? _line.Length : colon;
            var valueStart = colon < 0 ? _line.Length : colon + 1;
            if (valueStart < _line.Length && _line[valueStart] == ' ')
                valueStart++;

            if (FieldIs("data", nameLength))
            {
                if (_hasData)
                    _data.Append('\n');
                _data.Append(_line, valueStart, _line.Length - valueStart);
                _hasData = true;
            }
            else if (FieldIs("event", nameLength))
            {
                _eventName = _line.ToString(valueStart, _line.Length - valueStart);
            }
        }

        void Dispatch(Queue<SseEvent> output)
        {
            if (_hasData)
                output.Enqueue(new SseEvent(_eventName, _data.ToString()));
            _data.Clear();
            _hasData = false;
            _eventName = null;
        }

        int IndexOfColon()
        {
            for (var i = 0; i < _line.Length; i++)
            {
                if (_line[i] == ':')
                    return i;
            }
            return -1;
        }

        bool FieldIs(string name, int length)
        {
            if (length != name.Length)
                return false;
            for (var i = 0; i < length; i++)
            {
                if (_line[i] != name[i])
                    return false;
            }
            return true;
        }
    }
}
