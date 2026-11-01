using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Health;
using Jolly.Testing;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Offline
{
    public sealed class MetaHealthProbeTests
    {
        sealed class ScriptedStatusClient : IHttpStatusClient
        {
            public long Status = 200;
            public Exception Throw;
            public bool Hold;
            public int Calls;
            public string LastUrl;
            public string LastBearer;
            public CancellationToken LastToken;

            public UniTask<long> GetStatus(string url, string bearerToken, CancellationToken ct)
            {
                Calls++;
                LastUrl = url;
                LastBearer = bearerToken;
                LastToken = ct;
                if (Throw != null)
                    return UniTask.FromException<long>(Throw);
                if (!Hold)
                    return UniTask.FromResult(Status);
                var pending = new UniTaskCompletionSource<long>();
                ct.Register(() => pending.TrySetCanceled(ct));
                return pending.Task;
            }
        }

        ScriptedStatusClient _http;
        FakeClock _clock;
        InMemorySecrets _secrets;
        RecordingLog _log;
        int _unauthorized;

        [SetUp]
        public void SetUp()
        {
            _http = new ScriptedStatusClient();
            _clock = new FakeClock();
            _secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "LLM_1_secret");
            _log = new RecordingLog();
            _unauthorized = 0;
        }

        MetaHealthProbe Create(string baseUrl = "https://api.meta.ai")
        {
            var probe = new MetaHealthProbe(_http, _secrets, _clock, _log, baseUrl);
            probe.Unauthorized += () => _unauthorized++;
            return probe;
        }

        static bool Result(UniTask<bool> ping)
        {
            Assert.That(ping.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            return ping.GetAwaiter().GetResult();
        }

        [TestCase("https://api.meta.ai")]
        [TestCase("https://api.meta.ai/")]
        public void Ping_GetsModelsWithTheBearerKey(string baseUrl)
        {
            var probe = Create(baseUrl);

            var ok = Result(probe.Ping(CancellationToken.None));

            Assert.That(ok, Is.True);
            Assert.That(_http.LastUrl, Is.EqualTo("https://api.meta.ai/v1/models"));
            Assert.That(_http.LastBearer, Is.EqualTo("LLM_1_secret"));
        }

        [TestCase(500)]
        [TestCase(404)]
        [TestCase(0)]
        public void Ping_OnAnyOtherStatus_ReturnsFalse(long status)
        {
            _http.Status = status;

            Assert.That(Result(Create().Ping(CancellationToken.None)), Is.False);
            Assert.That(_unauthorized, Is.Zero);
        }

        [Test]
        public void Ping_On401_ReturnsFalseAndRaisesUnauthorized()
        {
            _http.Status = 401;

            Assert.That(Result(Create().Ping(CancellationToken.None)), Is.False);
            Assert.That(_unauthorized, Is.EqualTo(1));
        }

        [Test]
        public void Ping_WithoutAKey_ReturnsFalseWithoutARequest()
        {
            _secrets = new InMemorySecrets();

            Assert.That(Result(Create().Ping(CancellationToken.None)), Is.False);
            Assert.That(_http.Calls, Is.Zero);
        }

        [Test]
        public void Ping_WhenTheClientThrows_ReturnsFalseAndLogsWithoutTheKey()
        {
            _http.Throw = new InvalidOperationException("boom");

            Assert.That(Result(Create().Ping(CancellationToken.None)), Is.False);
            Assert.That(_log.Contains(LogLevel.Warning, "evt=health.ping_failed"), Is.True);
            foreach (var entry in _log.Entries)
                Assert.That(entry.Message, Does.Not.Contain("LLM_1_secret"));
        }

        [Test]
        public void Ping_After3Seconds_TimesOutAndCancelsTheRequest()
        {
            _http.Hold = true;
            var ping = Create().Ping(CancellationToken.None);

            _clock.Advance(TimeSpan.FromSeconds(2.9));
            Assert.That(ping.Status, Is.EqualTo(UniTaskStatus.Pending));
            _clock.Advance(TimeSpan.FromSeconds(0.1));

            Assert.That(Result(ping), Is.False);
            Assert.That(_http.LastToken.IsCancellationRequested, Is.True);
        }

        [Test]
        public void Ping_WhenAnswered_StopsItsTimeoutTimer()
        {
            Result(Create().Ping(CancellationToken.None));

            Assert.That(_clock.PendingDelays, Is.Zero);
        }

        [Test]
        public void Ping_WhenTheCallerCancels_Throws()
        {
            _http.Hold = true;
            using var cts = new CancellationTokenSource();
            var ping = Create().Ping(cts.Token);

            cts.Cancel();

            Assert.That(ping.Status, Is.EqualTo(UniTaskStatus.Canceled));
        }
    }
}
