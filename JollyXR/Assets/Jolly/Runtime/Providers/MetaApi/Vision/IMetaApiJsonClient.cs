using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Providers.MetaApi.Vision
{
    /// <summary>
    /// The one HTTP capability SAM needs: a non-streaming JSON POST to the Meta API. The implementation owns the base URL
    /// (<c>https://api.meta.ai/v1/</c>), the bearer header, retries before the first byte and the HTTP status →
    /// <see cref="Jolly.Contracts.ProviderErrorKind"/> mapping (WP1's transport and MetaApi Common). It throws
    /// <see cref="Jolly.Contracts.ProviderException"/> for non-2xx, aborts the request when <paramref name="ct"/> is
    /// cancelled, and resumes on the main thread.
    /// </summary>
    public interface IMetaApiJsonClient
    {
        /// <param name="path">Relative to the API base, e.g. <c>responses</c>.</param>
        UniTask<string> PostJson(string path, string jsonBody, CancellationToken ct);
    }
}
