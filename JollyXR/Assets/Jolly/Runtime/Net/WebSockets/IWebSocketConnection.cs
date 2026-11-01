using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Net.WebSockets
{
    public enum WebSocketMessageKind { Text, Binary, Close }

    /// <summary>One whole received message. Text is set for Text; CloseStatus/CloseDescription for Close.</summary>
    public readonly struct WebSocketMessage
    {
        WebSocketMessage(WebSocketMessageKind kind, string text, int closeStatus, string closeDescription)
        { Kind = kind; Text = text; CloseStatus = closeStatus; CloseDescription = closeDescription; }

        public WebSocketMessageKind Kind { get; }
        public string Text { get; }
        public int CloseStatus { get; }
        public string CloseDescription { get; }

        public static WebSocketMessage FromText(string text) => new(WebSocketMessageKind.Text, text, 0, null);
        public static WebSocketMessage FromBinary() => new(WebSocketMessageKind.Binary, null, 0, null);
        public static WebSocketMessage FromClose(int status, string description) =>
            new(WebSocketMessageKind.Close, null, status, description);
    }

    /// <summary>
    /// A client WebSocket with one sender and one receiver allowed concurrently. Every UniTask resumes on the main
    /// thread. Failures surface as <c>ProviderException(Network)</c> under the provider name given at construction;
    /// cancelling any call aborts the connection. Dispose aborts without a close handshake.
    /// </summary>
    public interface IWebSocketConnection : IDisposable
    {
        UniTask ConnectAsync(Uri uri, CancellationToken ct);
        UniTask SendTextAsync(string text, CancellationToken ct);
        UniTask SendBinaryAsync(ArraySegment<byte> data, CancellationToken ct);
        /// <summary>The next whole message. After a Close message the connection has answered the close handshake.</summary>
        UniTask<WebSocketMessage> ReceiveAsync(CancellationToken ct);
    }
}
