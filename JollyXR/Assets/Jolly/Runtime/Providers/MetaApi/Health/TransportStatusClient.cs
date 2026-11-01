using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Net.Http;

namespace Jolly.Providers.MetaApi.Health
{
    /// <summary>The probe's status call over the shared <see cref="IHttpTransport"/> (one UnityWebRequest path in the app).</summary>
    public sealed class TransportStatusClient : IHttpStatusClient
    {
        readonly IHttpTransport _http;

        public TransportStatusClient(IHttpTransport http) => _http = http;

        public async UniTask<long> GetStatus(string url, string bearerToken, CancellationToken ct)
        {
            var request = new HttpRequestSpec(HttpRequestSpec.Get, url).WithHeader("Authorization", "Bearer " + bearerToken);
            try
            {
                return (await _http.Send(request, ct)).Status;
            }
            catch (HttpFailureException e)
            {
                return e.Status;
            }
        }
    }
}
