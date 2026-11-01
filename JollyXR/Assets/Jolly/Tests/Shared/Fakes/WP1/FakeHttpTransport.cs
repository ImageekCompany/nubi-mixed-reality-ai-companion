using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.Sse;

namespace Jolly.Testing
{
    /// <summary>
    /// Scripted IHttpTransport: each StreamSse enumeration consumes the next script. Synchronous unless a script hangs
    /// or waits on a clock, in which case an abort cancels it (as UnityWebRequest abort does). The first written event
    /// is the first byte.
    /// </summary>
    public sealed class FakeHttpTransport : IHttpTransport
    {
        delegate UniTask Script(IAsyncWriter<SseEvent> writer, CancellationToken ct);

        readonly Queue<Script> _scripts = new();
        readonly List<HttpRequestSpec> _requests = new();
        readonly List<CancellationToken> _tokens = new();

        public IReadOnlyList<HttpRequestSpec> Requests => _requests;
        /// <summary>Enumeration tokens per request; cancelled means the request was aborted.</summary>
        public IReadOnlyList<CancellationToken> Tokens => _tokens;
        /// <summary>Requests whose script has ended: completed, failed, aborted or abandoned by the consumer.</summary>
        public int Ended { get; private set; }
        /// <summary>Requests whose script was read to its end rather than aborted.</summary>
        public int ReadToEnd { get; private set; }

        public FakeHttpTransport Respond(IEnumerable<SseEvent> events) => Enqueue(async (w, _) =>
        {
            foreach (var e in events)
                await w.YieldAsync(e);
        });

        public FakeHttpTransport RespondAfter(IClock clock, TimeSpan delay, IEnumerable<SseEvent> events) => Enqueue(async (w, ct) =>
        {
            await clock.Delay(delay, ct);
            foreach (var e in events)
                await w.YieldAsync(e);
        });

        public FakeHttpTransport RespondThenHang(IEnumerable<SseEvent> events) => Enqueue(async (w, ct) =>
        {
            foreach (var e in events)
                await w.YieldAsync(e);
            await UniTask.Never(ct);
        });

        public FakeHttpTransport RespondThenFail(IEnumerable<SseEvent> events, Exception failure) => Enqueue(async (w, _) =>
        {
            foreach (var e in events)
                await w.YieldAsync(e);
            throw failure;
        });

        public FakeHttpTransport Fail(Exception failure) => Enqueue((_, _) => UniTask.FromException(failure));

        public FakeHttpTransport Hang() => Enqueue((_, ct) => UniTask.Never(ct));

        /// <summary>Sends a byte that completes no event (a partial line), then hangs.</summary>
        public FakeHttpTransport ByteThenHang() => Enqueue((w, ct) =>
        {
            (w as FirstByteWriter)?.Byte();
            return UniTask.Never(ct);
        });

        public UniTask<HttpResponse> Send(HttpRequestSpec request, CancellationToken ct) =>
            throw new NotSupportedException("Scripted streams only.");

        public IUniTaskAsyncEnumerable<SseEvent> StreamSse(HttpRequestSpec request, Action firstByte = null) =>
            UniTaskAsyncEnumerable.Create<SseEvent>(async (writer, ct) =>
            {
                _requests.Add(request);
                _tokens.Add(ct);
                try
                {
                    await _scripts.Dequeue()(firstByte == null ? writer : new FirstByteWriter(writer, firstByte), ct);
                    ReadToEnd++;
                }
                finally
                {
                    Ended++;
                }
            });

        sealed class FirstByteWriter : IAsyncWriter<SseEvent>
        {
            readonly IAsyncWriter<SseEvent> _inner;
            Action _firstByte;

            public FirstByteWriter(IAsyncWriter<SseEvent> inner, Action firstByte)
            {
                _inner = inner;
                _firstByte = firstByte;
            }

            public UniTask YieldAsync(SseEvent value)
            {
                Byte();
                return _inner.YieldAsync(value);
            }

            public void Byte()
            {
                var firstByte = _firstByte;
                _firstByte = null;
                firstByte?.Invoke();
            }
        }

        FakeHttpTransport Enqueue(Script script)
        {
            _scripts.Enqueue(script);
            return this;
        }
    }
}
