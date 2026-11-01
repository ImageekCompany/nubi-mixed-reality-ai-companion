using System.Text;
using Jolly.Contracts;
using Jolly.Net.Http;

namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>
    /// Base URL and bearer auth for api.meta.ai. The key comes from the secrets parsed at boot, so a re-pushed secrets
    /// file applies only after the app restarts (tools/device/push-secrets.sh force-stops it).
    /// </summary>
    public sealed class MetaApiEndpoint
    {
        public const string DefaultBaseUrl = "https://api.meta.ai/v1";

        readonly ISecretsProvider _secrets;
        readonly string _baseUrl;

        public MetaApiEndpoint(ISecretsProvider secrets, string baseUrl = DefaultBaseUrl)
        {
            _secrets = secrets;
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>Throws ProviderException(Unauthorized) when META_API_KEY is missing, before anything is sent.</summary>
        public HttpRequestSpec PostStreaming(string path, string json) =>
            Authorized(new HttpRequestSpec(HttpRequestSpec.Post, Url(path), Encoding.UTF8.GetBytes(json), "application/json"))
                .WithHeader("Accept", "text/event-stream");

        string Url(string path) => _baseUrl + "/" + path.TrimStart('/');

        HttpRequestSpec Authorized(HttpRequestSpec spec)
        {
            if (!_secrets.TryGet(SecretKeys.MetaApiKey, out var key))
                throw new ProviderException(MetaErrors.Provider, ProviderErrorKind.Unauthorized, "META_API_KEY is missing.");
            return spec.WithHeader("Authorization", "Bearer " + key);
        }
    }
}
