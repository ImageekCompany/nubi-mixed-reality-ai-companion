using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Net.Http;
using Jolly.Net.Sse;

namespace Jolly.Tests.EditMode.Net
{
    /// <summary>
    /// Scripted IHttpTransport: each StreamSse enumeration consumes the next script. Synchronous unless a script hangs,
    /// in which case it completes only when its token is cancelled (as UnityWebRequest abort does).
    /// It lives in the test assembly because Jolly.Testing may not reference Jolly.Net (asmdef graph).
    /// </summary>
    sealed class FakeHttpTransport : IHttpTransport
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

        public UniTask<HttpResponse> Send(HttpRequestSpec request, CancellationToken ct) =>
            throw new NotSupportedException("Scripted streams only.");

        public IUniTaskAsyncEnumerable<SseEvent> StreamSse(HttpRequestSpec request) =>
            UniTaskAsyncEnumerable.Create<SseEvent>(async (writer, ct) =>
            {
                _requests.Add(request);
                _tokens.Add(ct);
                try
                {
                    await _scripts.Dequeue()(writer, ct);
                    ReadToEnd++;
                }
                finally
                {
                    Ended++;
                }
            });

        FakeHttpTransport Enqueue(Script script)
        {
            _scripts.Enqueue(script);
            return this;
        }
    }
}
