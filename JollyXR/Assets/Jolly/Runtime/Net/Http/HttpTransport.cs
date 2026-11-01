using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Net.Sse;
using UnityEngine.Networking;

namespace Jolly.Net.Http
{
    /// <summary>
    /// <see cref="IHttpTransport"/> over UnityWebRequest. Main thread only. Cancellation is observed on the PlayerLoop
    /// (ToUniTask default path, UniTask.Yield without cancelImmediately), so Abort never runs on a foreign thread.
    /// Response sinks are reused across streams: a fresh SSE parser regrew its line buffer to the 5–10 KB echo events
    /// of every Responses stream (ADR-0023).
    /// </summary>
    public sealed class HttpTransport : IHttpTransport
    {
        const int StreamBufferBytes = 32 * 1024;
        const string RequestIdHeader = "x-request-id";

        readonly Stack<SseResponseSink> _sinks = new();

        public async UniTask<HttpResponse> Send(HttpRequestSpec spec, CancellationToken ct)
        {
            using var request = Create(spec, new DownloadHandlerBuffer());
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (UnityWebRequestException)
            {
                // Inspected below, with the body the error mapping needs.
            }
            var body = request.downloadHandler.text;
            ThrowIfFailed(request, body);
            return new HttpResponse((int)request.responseCode, body, request.GetResponseHeader(RequestIdHeader));
        }

        public IUniTaskAsyncEnumerable<SseEvent> StreamSse(HttpRequestSpec spec) =>
            UniTaskAsyncEnumerable.Create<SseEvent>((writer, ct) => Stream(spec, writer, ct));

        async UniTask Stream(HttpRequestSpec spec, IAsyncWriter<SseEvent> writer, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var buffer = ArrayPool<byte>.Shared.Rent(StreamBufferBytes);
            var sink = _sinks.Count > 0 ? _sinks.Pop() : new SseResponseSink();
            var handler = new SseDownloadHandler(buffer, sink);
            var request = Create(spec, handler);
            handler.Attach(request);
            try
            {
                // Completion is polled below (isDone) so that events and the end of the stream are seen in order.
                _ = request.SendWebRequest();
                while (true)
                {
                    // Read before draining: once done, every ReceiveData/CompleteContent callback has already run.
                    var done = request.isDone;
                    while (sink.TryTake(out var e))
                        await writer.YieldAsync(e);
                    if (done)
                        break;
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
                ThrowIfFailed(request, sink.ErrorBody);
            }
            finally
            {
                // Dispose aborts an in-flight request and disposes the handler before the buffer goes back to the pool.
                request.Dispose();
                ArrayPool<byte>.Shared.Return(buffer);
                sink.Reset();
                _sinks.Push(sink);
            }
        }

        static UnityWebRequest Create(HttpRequestSpec spec, DownloadHandler handler)
        {
            var request = new UnityWebRequest(spec.Url, spec.Method) { downloadHandler = handler };
            if (spec.Body != null)
                request.uploadHandler = new UploadHandlerRaw(spec.Body) { contentType = spec.ContentType };
            foreach (var header in spec.Headers)
                request.SetRequestHeader(header.Key, header.Value);
            return request;
        }

        static void ThrowIfFailed(UnityWebRequest request, string body)
        {
            if (request.result == UnityWebRequest.Result.Success)
                return;
            if (request.result != UnityWebRequest.Result.ProtocolError)
                throw HttpFailureException.Network(request.error);
            throw new HttpFailureException((int)request.responseCode, body, request.GetResponseHeader(RequestIdHeader),
                ParseRetryAfter(request.GetResponseHeader("Retry-After")));
        }

        static TimeSpan? ParseRetryAfter(string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
    }
}
