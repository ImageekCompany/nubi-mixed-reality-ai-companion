using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine.Networking;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// <see cref="IAsrFileClient"/> over UnityWebRequest. Awaited with the default <c>ToUniTask</c> cancellation path, so
    /// a cancel aborts the request on the PlayerLoop (contracts §0). The response is one small JSON body, so the
    /// buffered download handler is enough here.
    /// </summary>
    public sealed class UnityWebRequestAsrFileClient : IAsrFileClient
    {
        public async UniTask<string> PostAsync(Uri uri, byte[] body, string contentType, string apiKey, CancellationToken ct)
        {
            using var request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body) { contentType = contentType },
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: ct);
            }
            catch (UnityWebRequestException e)
            {
                throw new ProviderException(MuseAsrErrors.Provider, KindOf(e.ResponseCode),
                    $"Transcribe failed: HTTP {e.ResponseCode} {e.Error}", (int)e.ResponseCode, inner: e);
            }
            return request.downloadHandler.text;
        }

        static ProviderErrorKind KindOf(long status) => status switch
        {
            0 => ProviderErrorKind.Network,
            401 or 403 => ProviderErrorKind.Unauthorized,
            429 => ProviderErrorKind.RateLimited,
            >= 500 => ProviderErrorKind.Server,
            _ => ProviderErrorKind.BadRequest,
        };
    }
}
