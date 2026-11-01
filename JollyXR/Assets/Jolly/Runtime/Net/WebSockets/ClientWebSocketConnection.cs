using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Net.WebSockets
{
    /// <summary>
    /// <see cref="IWebSocketConnection"/> over <c>System.Net.WebSockets.ClientWebSocket</c>, verified on Quest IL2CPP
    /// by the device spike (R4 transport). Socket continuations may run on the thread pool, so every operation switches
    /// back to the main thread before it returns or throws; that switch is verified on device only (R4), as EditMode has
    /// no socket. The receive buffer is owned, not pooled: an aborted receive can still be writing into it after Dispose.
    /// </summary>
    public sealed class ClientWebSocketConnection : IWebSocketConnection
    {
        const int InitialReceiveBytes = 16 * 1024;

        readonly ClientWebSocket _socket = new();
        readonly string _provider;
        byte[] _receiveBuffer = new byte[InitialReceiveBytes];

        public ClientWebSocketConnection(string provider) => _provider = provider;

        public async UniTask ConnectAsync(Uri uri, CancellationToken ct)
        {
            Exception failure = null;
            try { await _socket.ConnectAsync(uri, ct); }
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
                do
                {
                    if (count == _receiveBuffer.Length)
                        Array.Resize(ref _receiveBuffer, WebSocketFailures.GrowReceiveBuffer(_provider, _receiveBuffer.Length));
                    result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(_receiveBuffer, count, _receiveBuffer.Length - count), ct);
                    count += result.Count;
                } while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

                if (result.MessageType == WebSocketMessageType.Close)
                    await AnswerCloseAsync(ct);
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
            _socket.Abort();
            _socket.Dispose();
        }

        async UniTask SendAsync(ArraySegment<byte> data, WebSocketMessageType type, CancellationToken ct)
        {
            Exception failure = null;
            try { await _socket.SendAsync(data, type, true, ct); }
            catch (Exception e) { failure = e; }
            await UniTask.SwitchToMainThread();
            ThrowIfFailed(failure, "send", ct);
        }

        async UniTask AnswerCloseAsync(CancellationToken ct)
        {
            if (_socket.State != WebSocketState.CloseReceived)
                return;
            try { await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, ct); }
            catch (Exception) { /* The peer already closed; the close message is what the caller needs. */ }
        }

        void ThrowIfFailed(Exception failure, string operation, CancellationToken ct)
        {
            if (failure != null)
                throw WebSocketFailures.Map(_provider, operation, failure, ct);
        }
    }
}
