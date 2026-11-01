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
        UnityWebRequest _request;

        public SseDownloadHandler(byte[] preallocatedBuffer, SseResponseSink sink) : base(preallocatedBuffer) => _sink = sink;

        public void Attach(UnityWebRequest request) => _request = request;

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data != null && dataLength > 0)
                _sink.Receive(_request.responseCode, new ReadOnlySpan<byte>(data, 0, dataLength));
            return true;
        }

        protected override void CompleteContent() => _sink.Complete();
    }
}
