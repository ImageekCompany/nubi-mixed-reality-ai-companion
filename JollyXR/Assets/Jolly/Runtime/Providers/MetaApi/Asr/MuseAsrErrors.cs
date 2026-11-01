using System;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Server error frames and close codes → <see cref="ProviderException"/>. Mapping from the live probes and the
    /// documented codes (meta-model-api.md §5.2): 1008 carries auth and request errors, 1011 is internal (retry;
    /// also "Max session duration reached"), 1013 is rate limiting. "Audio backlog too large" means this client paced
    /// badly, so it is a Protocol bug, not a user error.
    /// </summary>
    public static class MuseAsrErrors
    {
        public const string Provider = "meta.asr";

        public static ProviderException FromEvent(in MuseAsrEvent e)
        {
            var message = e.Message ?? "ASR error";
            var kind =
                e.ErrorType == "authentication_error" || e.ErrorCode == "invalid_api_key" ? ProviderErrorKind.Unauthorized
                : Contains(message, "backlog") ? ProviderErrorKind.Protocol
                : Contains(e.ErrorType, "rate_limit") ? ProviderErrorKind.RateLimited
                : e.ErrorParam != null || Contains(message, "Malformed") ? ProviderErrorKind.BadRequest
                : ProviderErrorKind.Server;
            return new ProviderException(Provider, kind, message, requestId: e.SessionId);
        }

        /// <summary>A close that arrived before the final transcript.</summary>
        public static ProviderException FromClose(int status, string description, string sessionId)
        {
            var kind = status switch
            {
                1008 when Contains(description, "Unauthorized") => ProviderErrorKind.Unauthorized,
                1008 when Contains(description, "backlog") => ProviderErrorKind.Protocol,
                1008 => ProviderErrorKind.BadRequest,
                1011 => ProviderErrorKind.Server,
                1013 => ProviderErrorKind.RateLimited,
                1000 => ProviderErrorKind.Protocol,
                _ => ProviderErrorKind.Network,
            };
            return new ProviderException(Provider, kind, $"ASR socket closed {status} \"{description}\" before the final transcript.",
                requestId: sessionId);
        }

        static bool Contains(string text, string fragment) =>
            text != null && text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
