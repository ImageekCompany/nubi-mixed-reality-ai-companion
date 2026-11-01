using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.Http;
using Jolly.Net.Retry;

namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>
    /// Opens a Meta SSE stream (Responses chat, SAM) with retries only until the first event: once an event has been
    /// handed out, a retry would replay output the caller may already have spoken.
    /// </summary>
    public sealed class MetaSseClient
    {
        readonly IHttpTransport _transport;
        readonly RetryExecutor _retry;
        readonly IClock _clock;

        public MetaSseClient(IHttpTransport transport, RetryExecutor retry, IClock clock)
        {
            _transport = transport;
            _retry = retry;
            _clock = clock;
        }

        /// <summary>
        /// Completes once the first event has arrived; it is <see cref="MetaSseStream.Current"/>. Every attempt is
        /// counted in <paramref name="attempts"/>, also when Open throws.
        /// </summary>
        public UniTask<MetaSseStream> Open(HttpRequestSpec request, TimeSpan firstEventTimeout, TimeSpan idleTimeout,
                                           RetryPolicy policy, OpenAttempts attempts, CancellationToken ct) =>
            _retry.Execute(t => OpenOnce(request, firstEventTimeout, idleTimeout, attempts, t), policy, ct);

        async UniTask<MetaSseStream> OpenOnce(HttpRequestSpec request, TimeSpan firstEventTimeout, TimeSpan idleTimeout,
                                              OpenAttempts attempts, CancellationToken ct)
        {
            attempts.OnStarted();
            var stream = new MetaSseStream(_transport, request, new StreamTimeout(_clock, ct), firstEventTimeout, idleTimeout, ct);
            try
            {
                if (await stream.MoveNext())
                    return stream;
                throw MetaErrors.Protocol("stream ended before its first event");
            }
            catch (Exception e)
            {
                if (e is ProviderException { HttpStatus: >= 400 })
                    attempts.OnRejected();
                await stream.DisposeAsync();
                throw;
            }
        }
    }
}
