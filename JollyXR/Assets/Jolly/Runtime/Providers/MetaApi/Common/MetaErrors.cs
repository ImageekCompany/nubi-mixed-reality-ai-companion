using System;
using Jolly.Contracts;
using Jolly.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Providers.MetaApi.Common
{
    /// <summary>Maps HTTP failures and streamed error events to ProviderException (meta-model-api.md §6, docs error-handling).</summary>
    public static class MetaErrors
    {
        public const string Provider = "meta";

        public static ProviderException FromHttp(HttpFailureException e)
        {
            if (e.IsNetworkError)
                return new ProviderException(Provider, ProviderErrorKind.Network, e.Message, inner: e);
            var message = EnvelopeMessage(e.Body) is { } m ? $"HTTP {e.Status}: {m}" : $"HTTP {e.Status}";
            return new ProviderException(Provider, KindOfStatus(e.Status), message, e.Status, e.RequestId, e.RetryAfter, e);
        }

        /// <summary>A terminal response.failed or "error" event; code is machine-readable, message is server-sanitised.</summary>
        public static ProviderException FromStreamError(string code, string message, string requestId) =>
            new(Provider, KindOf(code), $"stream error {code ?? "unknown"}: {message}", requestId: requestId);

        public static ProviderException Timeout(string what, TimeSpan after) =>
            new(Provider, ProviderErrorKind.Timeout, $"{what} after {after.TotalSeconds:0.#} s");

        public static ProviderException Protocol(string message) => new(Provider, ProviderErrorKind.Protocol, message);

        /// <summary>The one HTTP status → ProviderErrorKind mapping of every MetaApi adapter; 0 is a connection error.</summary>
        public static ProviderErrorKind KindOfStatus(long status) => status switch
        {
            0 => ProviderErrorKind.Network,
            401 or 403 => ProviderErrorKind.Unauthorized,
            402 => ProviderErrorKind.BudgetExceeded,
            408 => ProviderErrorKind.Timeout,
            429 => ProviderErrorKind.RateLimited,
            >= 500 => ProviderErrorKind.Server,
            >= 400 => ProviderErrorKind.BadRequest,
            _ => ProviderErrorKind.Protocol,
        };

        static ProviderErrorKind KindOf(string code) => code switch
        {
            "rate_limit_exceeded" => ProviderErrorKind.RateLimited,
            "invalid_api_key" => ProviderErrorKind.Unauthorized,
            "gateway_timeout" => ProviderErrorKind.Timeout,
            "invalid_request_error" or "invalid_request" => ProviderErrorKind.BadRequest,
            _ => ProviderErrorKind.Server,
        };

        static string EnvelopeMessage(string body)
        {
            if (string.IsNullOrEmpty(body) || body[0] != '{')
                return null;
            try
            {
                var token = JObject.Parse(body).SelectToken("error.message");
                return token?.Type == JTokenType.String ? (string)token : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
