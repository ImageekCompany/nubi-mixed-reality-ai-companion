using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Net.Http;
using Jolly.Net.Sse;

namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>
    /// One open SSE response. Every event restarts the idle window; a silent window or an HTTP failure surfaces as
    /// ProviderException, the caller's cancellation as OperationCanceledException. DisposeAsync aborts the request.
    /// </summary>
    public sealed class MetaSseStream
    {
        readonly IUniTaskAsyncEnumerator<SseEvent> _events;
        readonly StreamTimeout _timeout;
        readonly TimeSpan _idleTimeout;
        readonly CancellationToken _ct;
        TimeSpan _window;
        bool _started;

        internal MetaSseStream(IHttpTransport transport, HttpRequestSpec request, StreamTimeout timeout,
                               TimeSpan firstEventTimeout, TimeSpan idleTimeout, CancellationToken ct)
        {
            _timeout = timeout;
            _idleTimeout = idleTimeout;
            _ct = ct;
            _events = transport.StreamSse(request).GetAsyncEnumerator(timeout.Token);
            Arm(firstEventTimeout);
        }

        public SseEvent Current { get; private set; }

        public async UniTask<bool> MoveNext()
        {
            try
            {
                if (!await _events.MoveNextAsync())
                    return false;
            }
            catch (OperationCanceledException) when (_timeout.HasFired && !_ct.IsCancellationRequested)
            {
                throw MetaErrors.Timeout(_started ? "no stream event" : "no first stream event", _window);
            }
            catch (HttpFailureException e)
            {
                throw MetaErrors.FromHttp(e);
            }
            Current = _events.Current;
            _started = true;
            Arm(_idleTimeout);
            return true;
        }

        public async UniTask DisposeAsync()
        {
            try
            {
                await _events.DisposeAsync();
            }
            finally
            {
                _timeout.Dispose();
            }
        }

        void Arm(TimeSpan window)
        {
            _window = window;
            _timeout.Restart(window);
        }
    }
}
