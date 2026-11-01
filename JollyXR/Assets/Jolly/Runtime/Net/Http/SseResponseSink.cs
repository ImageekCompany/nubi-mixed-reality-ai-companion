using System;
using System.Collections.Generic;
using System.Text;
using Jolly.Net.Sse;

namespace Jolly.Net.Http
{
    /// <summary>
    /// Routes a streamed HTTP body: bytes of a 2xx response go through <see cref="SseLineParser"/>, any other status
    /// is kept (capped) as the error body for status mapping. Main thread only.
    /// </summary>
    public sealed class SseResponseSink
    {
        readonly SseLineParser _parser = new();
        readonly Queue<SseEvent> _events = new();
        readonly byte[] _errorBody;
        int _errorLength;

        public SseResponseSink(int maxErrorBytes = 4096) => _errorBody = new byte[maxErrorBytes];

        public string ErrorBody => Encoding.UTF8.GetString(_errorBody, 0, _errorLength);

        public void Receive(long httpStatus, ReadOnlySpan<byte> bytes)
        {
            if (httpStatus is >= 200 and < 300)
            {
                _parser.Feed(bytes, _events);
                return;
            }
            var take = Math.Min(bytes.Length, _errorBody.Length - _errorLength);
            bytes.Slice(0, take).CopyTo(new Span<byte>(_errorBody, _errorLength, take));
            _errorLength += take;
        }

        public void Complete() => _parser.Complete(_events);

        public bool TryTake(out SseEvent e) => _events.TryDequeue(out e);
    }
}
