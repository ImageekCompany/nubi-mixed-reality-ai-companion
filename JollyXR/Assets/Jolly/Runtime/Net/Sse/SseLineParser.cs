using System;
using System.Collections.Generic;
using System.Text;

namespace Jolly.Net.Sse
{
    /// <summary>
    /// Incremental text/event-stream parser (WHATWG HTML §9.2.6) fed with raw bytes as they arrive. A chunk may split
    /// a UTF-8 sequence, a CRLF pair or a line anywhere. Allocates only each event's data string once its buffers have
    /// grown to the longest line: event names repeat, so they are reused. Not StringBuilder: its Clear reallocates a
    /// builder that grew past one chunk, which a Responses stream's 5–10 KB echo events did on every event (ADR-0023).
    /// </summary>
    public sealed class SseLineParser
    {
        const int DecodeChunk = 4096;
        const int MaxCachedEventNames = 32;

        readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
        // Not DecodeChunk: bytes the decoder carried over from the previous slice can add a char.
        readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(DecodeChunk)];
        readonly List<string> _eventNames = new(MaxCachedEventNames);
        char[] _line = new char[256];
        char[] _data = new char[1024];
        int _lineLength;
        int _dataLength;
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
            if (_lineLength > 0)
                EndLine(output);
            Dispatch(output);
            _afterCr = false;
        }

        /// <summary>Forgets a partial line or event and any split UTF-8 sequence; keeps the buffers.</summary>
        public void Reset()
        {
            _decoder.Reset();
            _lineLength = 0;
            _dataLength = 0;
            _eventName = null;
            _hasData = false;
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
            {
                EndLine(output);
                return;
            }
            if (_lineLength == _line.Length)
                Array.Resize(ref _line, _line.Length * 2);
            _line[_lineLength++] = c;
        }

        void EndLine(Queue<SseEvent> output)
        {
            if (_lineLength == 0)
                Dispatch(output);
            else if (_line[0] != ':')
                ApplyField();
            _lineLength = 0;
        }

        void ApplyField()
        {
            var line = new ReadOnlySpan<char>(_line, 0, _lineLength);
            var colon = line.IndexOf(':');
            var name = colon < 0 ? line : line.Slice(0, colon);
            var value = colon < 0 ? ReadOnlySpan<char>.Empty : line.Slice(colon + 1);
            if (!value.IsEmpty && value[0] == ' ')
                value = value.Slice(1);

            if (name.SequenceEqual("data".AsSpan()))
            {
                if (_hasData)
                    AppendData("\n".AsSpan());
                AppendData(value);
                _hasData = true;
            }
            else if (name.SequenceEqual("event".AsSpan()))
            {
                _eventName = EventName(value);
            }
        }

        void AppendData(ReadOnlySpan<char> text)
        {
            var needed = _dataLength + text.Length;
            if (needed > _data.Length)
                Array.Resize(ref _data, Math.Max(needed, _data.Length * 2));
            text.CopyTo(new Span<char>(_data, _dataLength, text.Length));
            _dataLength = needed;
        }

        /// <summary>A stream uses a handful of names; reusing them saves a string per event.</summary>
        string EventName(ReadOnlySpan<char> value)
        {
            foreach (var known in _eventNames)
            {
                if (value.SequenceEqual(known.AsSpan()))
                    return known;
            }
            var name = value.ToString();
            if (_eventNames.Count < MaxCachedEventNames)
                _eventNames.Add(name);
            return name;
        }

        void Dispatch(Queue<SseEvent> output)
        {
            if (_hasData)
                output.Enqueue(new SseEvent(_eventName, new string(_data, 0, _dataLength)));
            _dataLength = 0;
            _hasData = false;
            _eventName = null;
        }
    }
}
