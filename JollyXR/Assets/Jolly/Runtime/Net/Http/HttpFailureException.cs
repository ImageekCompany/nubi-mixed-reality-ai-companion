using System;

namespace Jolly.Net.Http
{
    /// <summary>
    /// A request that produced no usable response: a non-2xx status, or no response at all (Status 0). Provider
    /// adapters map it to ProviderException; the transport stays vendor-neutral.
    /// </summary>
    public sealed class HttpFailureException : Exception
    {
        public HttpFailureException(int status, string body, string requestId = null, TimeSpan? retryAfter = null)
            : base("HTTP " + status)
        { Status = status; Body = body ?? string.Empty; RequestId = requestId; RetryAfter = retryAfter; }

        HttpFailureException(string networkError) : base("network error: " + networkError) => Body = string.Empty;

        public static HttpFailureException Network(string error) => new(error);

        /// <summary>0 when no response arrived (DNS, connect, TLS, reset).</summary>
        public int Status { get; }
        /// <summary>Error body as sent by the server (capped by the transport).</summary>
        public string Body { get; }
        public string RequestId { get; }
        public TimeSpan? RetryAfter { get; }
        public bool IsNetworkError => Status == 0;
    }
}
