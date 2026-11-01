using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Health
{
    /// <summary>
    /// GET {base}/v1/models with the API key: true on 2xx. The 3 s timeout runs on IClock (PlayerLoop in the app,
    /// deterministic in tests) and cancels the request. A 401 also raises <see cref="Unauthorized"/>, because
    /// <see cref="IHealthProbe.Ping"/> can only answer reachable or not; wire it to IConnectivityMonitor.ReportFailure.
    /// </summary>
    public sealed class MetaHealthProbe : IHealthProbe
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

        const string LogCategory = "health";

        readonly IHttpStatusClient _http;
        readonly ISecretsProvider _secrets;
        readonly IClock _clock;
        readonly ILog _log;
        readonly string _url;
        readonly TimeSpan _timeout;

        public MetaHealthProbe(IHttpStatusClient http, ISecretsProvider secrets, IClock clock, ILog log,
                               string baseUrl, TimeSpan? timeout = null)
        {
            _http = http;
            _secrets = secrets;
            _clock = clock;
            _log = log;
            _url = baseUrl.TrimEnd('/') + "/v1/models";
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
                return status >= 200 && status < 300;
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
