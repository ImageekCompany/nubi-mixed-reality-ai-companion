using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Net.WebSockets;

namespace Jolly.Tests.EditMode.Asr
{
    /// <summary>
    /// Scripted server side. Sends complete synchronously and are recorded; receives are fed by <see cref="Push"/>.
    /// Cancelling a pending receive fails it, as a real socket abort does.
    /// </summary>
    sealed class FakeWebSocket : IWebSocketConnection
    {
        public readonly struct Frame
        {
            public Frame(string text, byte[] binary) { Text = text; Binary = binary; }
            public string Text { get; }
            public byte[] Binary { get; }
            public bool IsText => Text != null;
        }

        readonly Queue<WebSocketMessage> _inbox = new();
        readonly List<Frame> _sent = new();
        UniTaskCompletionSource<WebSocketMessage> _pendingReceive;
        CancellationTokenRegistration _pendingRegistration;

        public Uri ConnectedUri { get; private set; }
        public bool IsDisposed { get; private set; }
        public Exception SendFailure { get; set; }
        public IReadOnlyList<Frame> Sent => _sent;

        public UniTask ConnectAsync(Uri uri, CancellationToken ct)
        {
            ConnectedUri = uri;
            return UniTask.CompletedTask;
        }

        public UniTask SendTextAsync(string text, CancellationToken ct) => Record(new Frame(text, null), ct);

        public UniTask SendBinaryAsync(ArraySegment<byte> data, CancellationToken ct) =>
            Record(new Frame(null, data.AsSpan().ToArray()), ct);

        public UniTask<WebSocketMessage> ReceiveAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return UniTask.FromCanceled<WebSocketMessage>(ct);
            if (_inbox.Count > 0)
                return UniTask.FromResult(_inbox.Dequeue());

            var pending = new UniTaskCompletionSource<WebSocketMessage>();
            _pendingReceive = pending;
            _pendingRegistration = ct.Register(() => pending.TrySetCanceled(ct));
            return pending.Task;
        }

        public void Push(string json) => Deliver(WebSocketMessage.FromText(json));

        public void PushClose(int status, string description) => Deliver(WebSocketMessage.FromClose(status, description));

        public void Dispose() => IsDisposed = true;

        public int BinaryFrameCount()
        {
            var count = 0;
            foreach (var f in _sent)
                count += f.IsText ? 0 : 1;
            return count;
        }

        public int BinaryBytes()
        {
            var bytes = 0;
            foreach (var f in _sent)
                bytes += f.IsText ? 0 : f.Binary.Length;
            return bytes;
        }

        UniTask Record(Frame frame, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return UniTask.FromCanceled(ct);
            if (SendFailure != null)
                return UniTask.FromException(SendFailure);
            _sent.Add(frame);
            return UniTask.CompletedTask;
        }

        void Deliver(WebSocketMessage message)
        {
            var pending = _pendingReceive;
            if (pending == null)
            {
                _inbox.Enqueue(message);
                return;
            }
            _pendingReceive = null;
            _pendingRegistration.Dispose();
            pending.TrySetResult(message);
        }
    }
}
