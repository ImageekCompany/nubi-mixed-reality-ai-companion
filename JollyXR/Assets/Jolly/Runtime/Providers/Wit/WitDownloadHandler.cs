using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine.Networking;

namespace Jolly.Providers.Wit
{
    /// <summary>
    /// Streams the response body into pooled PCM chunks. Built on the preallocated-buffer constructor: the parameterless
    /// one allocates a new byte[] per ReceiveData. Runs on the main thread.
    /// </summary>
    sealed class WitDownloadHandler : DownloadHandlerScript
    {
        readonly WitPcmChunker _chunker = new(WitProtocol.SampleRate);
        readonly ChannelWriter<PcmChunk> _writer;

        public WitDownloadHandler(byte[] preallocatedBuffer, ChannelWriter<PcmChunk> writer) : base(preallocatedBuffer)
            => _writer = writer;

        public UnityWebRequest Owner { get; set; }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            // An error body is JSON, not audio; the status becomes a ProviderException when the request completes.
            var status = Owner.responseCode;
            if (status < 200 || status >= 300)
                return true;

            var chunk = _chunker.Feed(data, dataLength);
            if (chunk != null && !_writer.TryWrite(chunk))
                chunk.Dispose();
            return true;
        }
    }
}
