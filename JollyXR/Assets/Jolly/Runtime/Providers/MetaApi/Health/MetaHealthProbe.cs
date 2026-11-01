using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Health
{
    /// <summary>
    /// GET {base}/models (the base includes /v1) with the API key: true on 2xx, and on 429 since a rate-limited server is still reachable
    /// (ReportFailure(RateLimited) is state-neutral too). The 3 s timeout runs on IClock (PlayerLoop in the app,
    /// deterministic in tests) and cancels the request. A 401 also raises <see cref="Unauthorized"/>, because
    /// <see cref="IHealthProbe.Ping"/> can only answer reachable or not; wire it to IConnectivityMonitor.ReportFailure.
    /// </summary>
    public sealed class MetaHealthProbe : IHealthProbe
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

        const string LogCategory = "health";
        const long TooManyRequests = 429;

        readonly IHttpStatusClient _http;
        readonly ISecretsProvider _secrets;
        readonly IClock _clock;
        readonly ILog _log;
        readonly string _url;
        readonly TimeSpan _timeout;

        /// <param name="baseUrl">The API base including the version (MetaApiEndpoint.DefaultBaseUrl); /models is appended.</param>
        public MetaHealthProbe(IHttpStatusClient http, ISecretsProvider secrets, IClock clock, ILog log,
                               string baseUrl, TimeSpan? timeout = null)
        {
            _http = http;
            _secrets = secrets;
            _clock = clock;
            _log = log;
            _url = baseUrl.TrimEnd('/') + "/models";
            _timeout = timeout ?? DefaultTimeout;
        }

        public event Action Unauthorized;

        public async UniTask<bool> Ping(CancellationToken ct)
        {
            if (!_secrets.TryGet(SecretKeys.MetaApiKey, out var key))
                return false;

            using var request = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                ExpireAfterTimeout(request).Forget();
                var status = await _http.GetStatus(_url, key, request.Token);
                if (status == 401)
                    Unauthorized?.Invoke();
                var reached = status is >= 200 and < 300 or TooManyRequests;
                if (!reached)
                    Warn("status_" + status);
                return reached;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                Warn("timeout");
                return false;
            }
            catch (Exception e)
            {
                Warn(e.GetType().Name);
                return false;
            }
            finally
            {
                request.Cancel();
            }
        }

        async UniTaskVoid ExpireAfterTimeout(CancellationTokenSource request)
        {
            var token = request.Token;
            if (!await _clock.Delay(_timeout, token).SuppressCancellationThrow())
                request.Cancel();
        }

        void Warn(string reason)
        {
            if (_log.IsEnabled(LogLevel.Warning))
                _log.Write(LogLevel.Warning, LogCategory, "evt=health.ping_failed reason=" + reason);
        }
    }
}
