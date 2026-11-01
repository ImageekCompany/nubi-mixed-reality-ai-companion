using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;

namespace Jolly.Providers.MetaApi.Health
{
    /// <summary>Main thread only. No download handler, so the response body is discarded instead of buffered.</summary>
    public sealed class UnityWebRequestStatusClient : IHttpStatusClient
    {
        public async UniTask<long> GetStatus(string url, string bearerToken, CancellationToken ct)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (UnityWebRequestException)
            {
                // ConnectionError leaves responseCode at 0 and ProtocolError carries the status; both are answers here.
            }
            return request.responseCode;
        }
    }
}
