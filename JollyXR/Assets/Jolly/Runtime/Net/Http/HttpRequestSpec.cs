using System.Collections.Generic;

namespace Jolly.Net.Http
{
    /// <summary>A transport-neutral request. Headers may carry credentials: never log them or the body.</summary>
    public sealed class HttpRequestSpec
    {
        public const string Get = "GET";
        public const string Post = "POST";

        readonly List<KeyValuePair<string, string>> _headers = new(4);

        public HttpRequestSpec(string method, string url, byte[] body = null, string contentType = null)
        { Method = method; Url = url; Body = body; ContentType = contentType; }

        public string Method { get; }
        public string Url { get; }
        public byte[] Body { get; }
        public string ContentType { get; }
        public IReadOnlyList<KeyValuePair<string, string>> Headers => _headers;

        public HttpRequestSpec WithHeader(string name, string value)
        {
            _headers.Add(new KeyValuePair<string, string>(name, value));
            return this;
        }

        public bool TryGetHeader(string name, out string value)
        {
            foreach (var h in _headers)
            {
                if (string.Equals(h.Key, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    value = h.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
