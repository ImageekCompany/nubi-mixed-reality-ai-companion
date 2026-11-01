using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Net.Http;
using Jolly.Net.Sse;
using Jolly.Providers.MetaApi.Health;
using Jolly.Tests.EditMode.Core;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Offline
{
    /// <summary>The health probe reads a status, never an exception: an HTTP error status still proves the network is up.</summary>
    public sealed class TransportStatusClientTests
    {
        sealed class OneShotTransport : IHttpTransport
        {
            public Func<HttpRequestSpec, CancellationToken, UniTask<HttpResponse>> Respond;
            public HttpRequestSpec Last;

            public UniTask<HttpResponse> Send(HttpRequestSpec request, CancellationToken ct)
            {
                Last = request;
                return Respond(request, ct);
            }

            public IUniTaskAsyncEnumerable<SseEvent> StreamSse(HttpRequestSpec request, Action firstByte = null) =>
                throw new NotSupportedException("The probe never streams.");
        }

        readonly OneShotTransport _http = new();

        [Test]
        public void Ok_SendsAnAuthorisedGetAndReturnsTheStatus()
        {
            _http.Respond = (_, _) => UniTask.FromResult(new HttpResponse(200, "{}", "req_1"));

            var status = Sync.Run(new TransportStatusClient(_http).GetStatus("https://api.example/v1/models", "k-1", default));

            Assert.That(status, Is.EqualTo(200));
            Assert.That((_http.Last.Method, _http.Last.Url), Is.EqualTo((HttpRequestSpec.Get, "https://api.example/v1/models")));
            Assert.That(_http.Last.TryGetHeader("Authorization", out var auth), Is.True);
            Assert.That(auth, Is.EqualTo("Bearer k-1"));
        }

        [TestCase(401)]
        [TestCase(503)]
        public void HttpErrorStatus_IsReturnedNotThrown(int code)
        {
            _http.Respond = (_, _) => UniTask.FromException<HttpResponse>(new HttpFailureException(code, "{}"));

            Assert.That(Sync.Run(new TransportStatusClient(_http).GetStatus("https://x", "k", default)), Is.EqualTo(code));
        }

        [Test]
        public void NetworkError_IsStatusZero()
        {
            _http.Respond = (_, _) => UniTask.FromException<HttpResponse>(HttpFailureException.Network("Cannot resolve host"));

            Assert.That(Sync.Run(new TransportStatusClient(_http).GetStatus("https://x", "k", default)), Is.Zero);
        }

        [Test]
        public void Cancellation_PropagatesInsteadOfBecomingAStatus()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            _http.Respond = (_, ct) => UniTask.FromCanceled<HttpResponse>(ct);

            var task = new TransportStatusClient(_http).GetStatus("https://x", "k", cts.Token);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Canceled));
        }
    }
}
