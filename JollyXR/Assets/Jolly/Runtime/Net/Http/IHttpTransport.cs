using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Net.Sse;

namespace Jolly.Net.Http
{
    public readonly struct HttpResponse
    {
        public HttpResponse(int status, string body, string requestId) { Status = status; Body = body; RequestId = requestId; }
        public int Status { get; }
        public string Body { get; }
        public string RequestId { get; }
    }

    /// <summary>
    /// HTTP for the provider adapters. Everything resumes on the main thread. Failures throw
    /// <see cref="HttpFailureException"/>; cancellation throws OperationCanceledException and aborts the request.
    /// </summary>
    public interface IHttpTransport
    {
        /// <summary>Buffers the whole body. Throws on a non-2xx status.</summary>
        UniTask<HttpResponse> Send(HttpRequestSpec request, CancellationToken ct);

        /// <summary>
        /// Starts the request when enumeration starts and yields server-sent events as they arrive. A non-2xx status
        /// throws before any event. Cancelling the enumeration token, or disposing the enumerator, aborts the request.
        /// </summary>
        IUniTaskAsyncEnumerable<SseEvent> StreamSse(HttpRequestSpec request);
    }
}
