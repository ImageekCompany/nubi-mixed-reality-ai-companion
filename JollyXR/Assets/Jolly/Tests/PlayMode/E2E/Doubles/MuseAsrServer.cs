using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Net.WebSockets;
using Newtonsoft.Json.Linq;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>
    /// Muse realtime ASR at the WebSocket edge, speaking the recorded protocol (Tests/Fixtures/asr): the handshake gets a
    /// session ack, endStream gets the next queued final and a normal close. Each connection is one session.
    /// </summary>
    public sealed class MuseAsrServer
    {
        readonly Queue<string> _finals = new();

        public int Sessions { get; private set; }
        public int AudioFrames { get; private set; }

        /// <summary>The transcript of the next session; an empty string is a silent press.</summary>
        public MuseAsrServer Will(string final)
        {
            _finals.Enqueue(final);
            return this;
        }

        public IWebSocketConnection Connect(string provider) => new Connection(this);

        sealed class Connection : IWebSocketConnection
        {
            readonly MuseAsrServer _server;
            readonly Queue<WebSocketMessage> _inbox = new();
            UniTaskCompletionSource<WebSocketMessage> _pending;
            bool _handshakeSeen;

            public Connection(MuseAsrServer server) => _server = server;

            public UniTask ConnectAsync(Uri uri, CancellationToken ct)
            {
                _server.Sessions++;
                return UniTask.CompletedTask;
            }

            public UniTask SendTextAsync(string text, CancellationToken ct)
            {
                if (!_handshakeSeen)
                {
                    _handshakeSeen = true;
                    Deliver(WebSocketMessage.FromText("{\"sessionId\":\"e2e-session\"}"));
                }
                else if (text.Contains("endStream"))
                {
                    var final = _server._finals.Count > 0 ? _server._finals.Dequeue() : string.Empty;
                    Deliver(WebSocketMessage.FromText(new JObject
                    {
                        ["type"] = "transcript", ["transcript"] = final, ["final"] = true, ["audioProcessedMs"] = 1000,
                    }.ToString(Newtonsoft.Json.Formatting.None)));
                    Deliver(WebSocketMessage.FromClose(1000, "No more transcript segments"));
                }
                return UniTask.CompletedTask;
            }

            public UniTask SendBinaryAsync(ArraySegment<byte> data, CancellationToken ct)
            {
                _server.AudioFrames++;
                return UniTask.CompletedTask;
            }

            public UniTask<WebSocketMessage> ReceiveAsync(CancellationToken ct)
            {
                if (_inbox.Count > 0)
                    return UniTask.FromResult(_inbox.Dequeue());
                _pending = new UniTaskCompletionSource<WebSocketMessage>();
                var pending = _pending;
                ct.Register(() => pending.TrySetCanceled(ct));
                return pending.Task;
            }

            public void Dispose()
            {
                _pending?.TrySetCanceled();
            }

            void Deliver(WebSocketMessage message)
            {
                var pending = _pending;
                if (pending != null && pending.Task.Status == UniTaskStatus.Pending)
                {
                    _pending = null;
                    pending.TrySetResult(message);
                    return;
                }
                _inbox.Enqueue(message);
            }
        }
    }
}
