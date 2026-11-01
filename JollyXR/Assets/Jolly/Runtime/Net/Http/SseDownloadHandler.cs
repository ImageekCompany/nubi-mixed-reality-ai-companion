using System;
using UnityEngine.Networking;

namespace Jolly.Net.Http
{
    /// <summary>
    /// Streams the body into an <see cref="SseResponseSink"/>. Uses the preallocated-buffer constructor: the
    /// parameterless one allocates a new byte[] per ReceiveData. ReceiveData runs on the main thread.
    /// </summary>
    sealed class SseDownloadHandler : DownloadHandlerScript
    {
        readonly SseResponseSink _sink;
        Action _firstByte;
        UnityWebRequest _request;

        public SseDownloadHandler(byte[] preallocatedBuffer, SseResponseSink sink, Action firstByte) : base(preallocatedBuffer)
        {
            _sink = sink;
            _firstByte = firstByte;
        }

        public void Attach(UnityWebRequest request) => _request = request;

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
                return true;
            if (_firstByte != null)
            {
                var firstByte = _firstByte;
                _firstByte = null;
                firstByte();
            }
            _sink.Receive(_request.responseCode, new ReadOnlySpan<byte>(data, 0, dataLength));
            return true;
        }

        protected override void CompleteContent() => _sink.Complete();
    }
}
