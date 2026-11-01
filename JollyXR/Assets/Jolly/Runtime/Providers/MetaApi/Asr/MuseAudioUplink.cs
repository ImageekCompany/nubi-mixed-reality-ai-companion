using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.WebSockets;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Forwards PCM chunks as binary s16le frames (frame boundaries carry no meaning to the server), then
    /// <c>endStream</c> in the same continuation that observes the source completing, so no frame passes in between.
    /// Chunks are packed into frames of at least <c>minFrameSeconds</c>: every send costs a 16 KB buffer in Mono's TLS
    /// stream (ADR-0023), and only partial transcripts wait for the packing, never the final, because what is pending
    /// goes out as soon as the source completes. Disposes every chunk it takes. One reused frame buffer: a send
    /// completes before the next one starts.
    /// </summary>
    sealed class MuseAudioUplink
    {
        readonly IWebSocketConnection _socket;
        readonly IClock _clock;
        readonly int _sampleRate;
        readonly int _minFrameSamples;
        readonly AudioSendPacer _pacer;
        byte[] _frame = new byte[8192];
        int _pendingSamples;

        public MuseAudioUplink(IWebSocketConnection socket, IClock clock, int sampleRate, double maxAheadSeconds,
                               double minFrameSeconds)
        {
            _socket = socket;
            _clock = clock;
            _sampleRate = sampleRate;
            _minFrameSamples = Math.Max(1, (int)Math.Round(minFrameSeconds * sampleRate));
            _pacer = new AudioSendPacer(maxAheadSeconds);
        }

        public double SentSeconds => _pacer.SentSeconds;
        public double EndStreamTime { get; private set; } = double.NaN;

        public async UniTask RunAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, CancellationToken ct)
        {
            await foreach (var chunk in audio.WithCancellation(ct))
            {
                using (chunk)
                    Append(chunk);
                if (_pendingSamples >= _minFrameSamples)
                    await SendPendingAsync(ct);
            }
            if (_pendingSamples > 0)
                await SendPendingAsync(ct);
            await _socket.SendTextAsync(MuseAsrHandshake.EndStream, ct);
            EndStreamTime = _clock.Now;
        }

        void Append(PcmChunk chunk)
        {
            if (chunk.SampleRate != _sampleRate)
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.BadRequest,
                    $"Chunk at {chunk.SampleRate} Hz on a {_sampleRate} Hz session.");
            var bytes = (_pendingSamples + chunk.Length) * 2;
            if (_frame.Length < bytes)
                Array.Resize(ref _frame, Math.Max(bytes, _frame.Length * 2));
            WriteLittleEndian(chunk.Span, _frame, _pendingSamples * 2);
            _pendingSamples += chunk.Length;
        }

        async UniTask SendPendingAsync(CancellationToken ct)
        {
            var seconds = (double)_pendingSamples / _sampleRate;
            var wait = _pacer.DelayBeforeSending(seconds, _clock.Now);
            if (wait > 0)
                await _clock.Delay(TimeSpan.FromSeconds(wait), ct);

            var bytes = _pendingSamples * 2;
            _pendingSamples = 0;
            await _socket.SendBinaryAsync(new ArraySegment<byte>(_frame, 0, bytes), ct);
            _pacer.OnSent(seconds, _clock.Now);
        }

        static void WriteLittleEndian(ReadOnlySpan<short> samples, byte[] destination, int offset)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                var s = samples[i];
                destination[offset + 2 * i] = (byte)s;
                destination[offset + 2 * i + 1] = (byte)(s >> 8);
            }
        }
    }
}
