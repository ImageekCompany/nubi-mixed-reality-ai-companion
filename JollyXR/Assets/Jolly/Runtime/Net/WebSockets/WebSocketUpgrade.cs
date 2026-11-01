using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Net.WebSockets
{
    /// <summary>
    /// The client side of the WebSocket opening handshake (RFC 6455 §4.1) over an already-open stream. It exists because
    /// Mono's <c>ClientWebSocket</c> reads the 101 response one byte per TLS read, and Mono's TLS stream allocates a
    /// fresh 16 KB buffer per read: each ASR connect allocated 4–10 MB on Quest (ADR-0023). Here the response is read in
    /// one or two reads into a reused buffer.
    /// </summary>
    public sealed class WebSocketUpgrade
    {
        const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        const int MaxResponseBytes = 8 * 1024;

        readonly byte[] _response = new byte[MaxResponseBytes];

        public WebSocketUpgrade(string key) => Key = key;

        /// <summary>The Sec-WebSocket-Key; base64 of 16 random bytes.</summary>
        public string Key { get; }

        public static WebSocketUpgrade WithRandomKey()
        {
            var nonce = new byte[16];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(nonce);
            return new WebSocketUpgrade(Convert.ToBase64String(nonce));
        }

        public byte[] Request(Uri uri)
        {
            var host = uri.IsDefaultPort ? uri.Host : uri.Host + ":" + uri.Port;
            var text = new StringBuilder(256)
                .Append("GET ").Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n")
                .Append("Host: ").Append(host).Append("\r\n")
                .Append("Upgrade: websocket\r\nConnection: Upgrade\r\n")
                .Append("Sec-WebSocket-Key: ").Append(Key).Append("\r\n")
                .Append("Sec-WebSocket-Version: 13\r\n\r\n");
            return Encoding.ASCII.GetBytes(text.ToString());
        }

        /// <summary>
        /// Reads the response head and checks it is a 101 with the matching Sec-WebSocket-Accept; throws
        /// <see cref="WebSocketUpgradeException"/> otherwise. Returns the bytes read past the head (normally none: the
        /// server speaks after the client's first message), which belong to the first frame.
        /// </summary>
        public async UniTask<ArraySegment<byte>> ReadResponseAsync(Stream stream, CancellationToken ct)
        {
            var length = 0;
            while (true)
            {
                if (length == _response.Length)
                    throw new WebSocketUpgradeException($"Upgrade response head longer than {MaxResponseBytes} bytes.");
                var read = await stream.ReadAsync(_response, length, _response.Length - length, ct);
                if (read == 0)
                    throw new WebSocketUpgradeException("Connection closed during the upgrade.");
                var searchFrom = Math.Max(0, length - 3);
                length += read;
                var end = IndexOfHeadEnd(_response, searchFrom, length);
                if (end < 0)
                    continue;
                Validate(Encoding.ASCII.GetString(_response, 0, end));
                return new ArraySegment<byte>(_response, end + 4, length - end - 4);
            }
        }

        /// <summary>Throws <see cref="WebSocketUpgradeException"/> unless the head accepts this upgrade.</summary>
        public void Validate(string head)
        {
            var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var status = lines[0].Split(' ');
            if (status.Length < 2 || !status[0].StartsWith("HTTP/1.", StringComparison.Ordinal))
                throw new WebSocketUpgradeException("Not an HTTP response: " + lines[0]);
            if (status[1] != "101")
                throw new WebSocketUpgradeException("Upgrade refused: " + lines[0], ParseStatus(status[1]));

            string accept = null;
            for (var i = 1; i < lines.Length; i++)
            {
                var colon = lines[i].IndexOf(':');
                if (colon > 0 && string.Equals(lines[i].Substring(0, colon).Trim(), "Sec-WebSocket-Accept",
                        StringComparison.OrdinalIgnoreCase))
                    accept = lines[i].Substring(colon + 1).Trim();
            }
            if (accept != ExpectedAccept())
                throw new WebSocketUpgradeException("Sec-WebSocket-Accept does not match the key.");
        }

        string ExpectedAccept()
        {
            using var sha1 = SHA1.Create();
            return Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(Key + AcceptGuid)));
        }

        static int IndexOfHeadEnd(byte[] buffer, int from, int length)
        {
            for (var i = from; i + 3 < length; i++)
            {
                if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n')
                    return i;
            }
            return -1;
        }

        static int ParseStatus(string code) => int.TryParse(code, out var status) ? status : 0;
    }

    public sealed class WebSocketUpgradeException : Exception
    {
        public WebSocketUpgradeException(string message, int httpStatus = 0) : base(message) => HttpStatus = httpStatus;

        /// <summary>The status of a refused upgrade; 0 when there was none.</summary>
        public int HttpStatus { get; }
    }
}
