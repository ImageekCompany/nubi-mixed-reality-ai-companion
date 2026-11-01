using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Net.Http;
using Jolly.Net.Sse;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>
    /// The Meta Model API at the HTTP edge: each streamed request is answered from the queue of its kind (agent chat,
    /// vision, SAM), chosen from the request body, so parallel SAM + vision calls never depend on start order.
    /// An empty queue fails the request like a server error would.
    /// </summary>
    public sealed class RoutingHttpTransport : IHttpTransport
    {
        public enum Kind { Chat, Vision, Sam }

        readonly Dictionary<Kind, Queue<List<SseEvent>>> _scripts = new()
        {
            [Kind.Chat] = new(), [Kind.Vision] = new(), [Kind.Sam] = new(),
        };
        readonly List<Kind> _requests = new();

        public IReadOnlyList<Kind> Requests => _requests;
        /// <summary>Requests whose enumeration was cancelled (an aborted UnityWebRequest on device).</summary>
        public int Aborted { get; private set; }

        public RoutingHttpTransport Enqueue(Kind kind, List<SseEvent> events)
        {
            _scripts[kind].Enqueue(events);
            return this;
        }

        public int Count(Kind kind)
        {
            var count = 0;
            foreach (var k in _requests)
                count += k == kind ? 1 : 0;
            return count;
        }

        public UniTask<HttpResponse> Send(HttpRequestSpec request, CancellationToken ct) =>
            throw new NotSupportedException("The app only streams.");

        public IUniTaskAsyncEnumerable<SseEvent> StreamSse(HttpRequestSpec request, Action firstByte = null) =>
            UniTaskAsyncEnumerable.Create<SseEvent>(async (writer, ct) =>
            {
                var kind = KindOf(request);
                _requests.Add(kind);
                if (_scripts[kind].Count == 0)
                    throw new HttpFailureException(500, "{\"error\":{\"message\":\"no script for " + kind + "\"}}");
                try
                {
                    foreach (var e in _scripts[kind].Dequeue())
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                        await writer.YieldAsync(e);
                    }
                }
                catch (OperationCanceledException)
                {
                    Aborted++;
                    throw;
                }
            });

        static Kind KindOf(HttpRequestSpec request)
        {
            var body = request.Body == null ? string.Empty : Encoding.UTF8.GetString(request.Body);
            if (body.Contains("\"sam-3.1\""))
                return Kind.Sam;
            return body.Contains("input_image") ? Kind.Vision : Kind.Chat;
        }
    }
}
