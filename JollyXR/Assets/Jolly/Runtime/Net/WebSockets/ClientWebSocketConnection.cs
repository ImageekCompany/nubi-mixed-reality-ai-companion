using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Net.WebSockets
{
    /// <summary>
    /// <see cref="IWebSocketConnection"/> over the BCL's managed WebSocket (<c>WebSocket.CreateFromStream</c>), verified on
    /// Quest IL2CPP (R4 transport, ADR-0023). The connection is opened here rather than by <c>ClientWebSocket</c>: socket,
    /// TLS (<c>SslStream</c>, the same UnityTls path ClientWebSocket uses) and <see cref="WebSocketUpgrade"/>, because
    /// Mono's ClientWebSocket reads the upgrade response one byte per TLS read at 16 KB of garbage each. Socket
    /// continuations may run on the thread pool, so every operation switches back to the main thread before it returns
    /// or throws; that switch is verified on device only, as EditMode has no socket. The receive buffer is owned, not
    /// pooled: an aborted receive can still be writing into it after Dispose.
    /// </summary>
    public sealed class ClientWebSocketConnection : IWebSocketConnection
    {
        const int InitialReceiveBytes = 16 * 1024;

        readonly string _provider;
        byte[] _receiveBuffer = new byte[InitialReceiveBytes];
        Socket _tcp;
        SslStream _tls;
        WebSocket _socket;
        bool _disposed;

        public ClientWebSocketConnection(string provider) => _provider = provider;

        public async UniTask ConnectAsync(Uri uri, CancellationToken ct)
        {
            Exception failure = null;
            try
            {
                await OpenAsync(uri, ct);
            }
            catch (Exception e) { failure = e; }
            await UniTask.SwitchToMainThread();
            ThrowIfFailed(failure, "connect", ct);
        }

        public UniTask SendTextAsync(string text, CancellationToken ct) =>
            SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, ct);

        public UniTask SendBinaryAsync(ArraySegment<byte> data, CancellationToken ct) =>
            SendAsync(data, WebSocketMessageType.Binary, ct);

        public async UniTask<WebSocketMessage> ReceiveAsync(CancellationToken ct)
        {
            Exception failure = null;
            WebSocketReceiveResult result = null;
            var count = 0;
            try
            {
                var socket = Connected();
                do
                {
                    if (count == _receiveBuffer.Length)
                        Array.Resize(ref _receiveBuffer, WebSocketFailures.GrowReceiveBuffer(_provider, _receiveBuffer.Length));
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(_receiveBuffer, count, _receiveBuffer.Length - count), ct);
                    count += result.Count;
                } while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

                if (result.MessageType == WebSocketMessageType.Close)
                    await AnswerCloseAsync(socket, ct);
            }
            catch (Exception e) { failure = e; }
            await UniTask.SwitchToMainThread();
            ThrowIfFailed(failure, "receive", ct);

            return result.MessageType switch
            {
                WebSocketMessageType.Close => WebSocketMessage.FromClose(
                    (int?)result.CloseStatus ?? (int)WebSocketCloseStatus.Empty, result.CloseStatusDescription),
                WebSocketMessageType.Text => WebSocketMessage.FromText(Encoding.UTF8.GetString(_receiveBuffer, 0, count)),
                _ => WebSocketMessage.FromBinary(),
            };
        }

        public void Dispose()
        {
            _disposed = true;
            _socket?.Abort();
            _socket?.Dispose();
            _tls?.Dispose();
            _tcp?.Dispose();
        }

        /// <summary>DNS, then the first address that accepts, as Mono's ClientWebSocket did; cancelling closes the socket.</summary>
        async UniTask OpenAsync(Uri uri, CancellationToken ct)
        {
            if (uri.Scheme != "wss")
                throw new ArgumentException("Only wss:// is supported.", nameof(uri));
            var port = uri.IsDefaultPort ? 443 : uri.Port;
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost);
            ct.ThrowIfCancellationRequested();
            Exception lastFailure = null;
            foreach (var address in addresses)
            {
                var tcp = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    using (ct.Register(static s => ((Socket)s).Dispose(), tcp))
                        await tcp.ConnectAsync(address, port);
                    ct.ThrowIfCancellationRequested();
                    _tcp = tcp;
                    break;
                }
                catch (Exception e) when (e is SocketException or ObjectDisposedException)
                {
                    tcp.Dispose();
                    ct.ThrowIfCancellationRequested();
                    lastFailure = e;
                }
            }
            if (_tcp == null)
                throw lastFailure ?? new WebSocketUpgradeException("No address for " + uri.DnsSafeHost);
            if (_disposed)
                throw new ObjectDisposedException(nameof(ClientWebSocketConnection));

            _tls = new SslStream(new NetworkStream(_tcp, ownsSocket: true), leaveInnerStreamOpen: false);
            using (ct.Register(static s => ((SslStream)s).Dispose(), _tls))
            {
                await _tls.AuthenticateAsClientAsync(uri.DnsSafeHost);
                var upgrade = WebSocketUpgrade.WithRandomKey();
                var request = upgrade.Request(uri);
                await _tls.WriteAsync(request, 0, request.Length, ct);
                await _tls.FlushAsync(ct);
                var early = await upgrade.ReadResponseAsync(_tls, ct);
                var stream = early.Count > 0 ? new PrefixedStream(early, _tls) : (System.IO.Stream)_tls;
                _socket = WebSocket.CreateFromStream(stream, false, null, WebSocket.DefaultKeepAliveInterval);
            }
            ct.ThrowIfCancellationRequested();
        }

        WebSocket Connected() => _socket ?? throw new InvalidOperationException("Not connected.");

        async UniTask SendAsync(ArraySegment<byte> data, WebSocketMessageType type, CancellationToken ct)
        {
            Exception failure = null;
            try { await Connected().SendAsync(data, type, true, ct); }
            catch (Exception e) { failure = e; }
            await UniTask.SwitchToMainThread();
            ThrowIfFailed(failure, "send", ct);
        }

        static async UniTask AnswerCloseAsync(WebSocket socket, CancellationToken ct)
        {
            if (socket.State != WebSocketState.CloseReceived)
                return;
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct); }
            catch (Exception) { /* The peer already closed; the close message is what the caller needs. */ }
        }

        void ThrowIfFailed(Exception failure, string operation, CancellationToken ct)
        {
            if (failure != null)
                throw WebSocketFailures.Map(_provider, operation, failure, ct);
        }
    }
}
