using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>HTTP seam of the file fallback. Returns the response body; failures are ProviderExceptions.</summary>
    public interface IAsrFileClient
    {
        UniTask<string> PostAsync(Uri uri, byte[] body, string contentType, string apiKey, CancellationToken ct);
    }
}
