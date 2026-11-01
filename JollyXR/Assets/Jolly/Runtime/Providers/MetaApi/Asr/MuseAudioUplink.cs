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
    /// Disposes every chunk it takes. One reused frame buffer: a send completes before the next one starts.
    /// </summary>
    sealed class MuseAudioUplink
    {
        readonly IWebSocketConnection _socket;
        readonly IClock _clock;
        readonly int _sampleRate;
        readonly AudioSendPacer _pacer;
        byte[] _frame = new byte[4096];

        public MuseAudioUplink(IWebSocketConnection socket, IClock clock, int sampleRate, double maxAheadSeconds)
        {
            _socket = socket;
            _clock = clock;
            _sampleRate = sampleRate;
            _pacer = new AudioSendPacer(maxAheadSeconds);
        }

        public double SentSeconds => _pacer.SentSeconds;
        public double EndStreamTime { get; private set; } = double.NaN;

        public async UniTask RunAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, CancellationToken ct)
        {
            await foreach (var chunk in audio.WithCancellation(ct))
            {
                using (chunk)
                    await SendAsync(chunk, ct);
            }
            await _socket.SendTextAsync(MuseAsrHandshake.EndStream, ct);
            EndStreamTime = _clock.Now;
        }

        async UniTask SendAsync(PcmChunk chunk, CancellationToken ct)
        {
            if (chunk.SampleRate != _sampleRate)
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.BadRequest,
                    $"Chunk at {chunk.SampleRate} Hz on a {_sampleRate} Hz session.");
            if (chunk.Length == 0)
                return;

            var seconds = (double)chunk.Length / _sampleRate;
            var wait = _pacer.DelayBeforeSending(seconds, _clock.Now);
            if (wait > 0)
                await _clock.Delay(TimeSpan.FromSeconds(wait), ct);

            var bytes = chunk.Length * 2;
            if (_frame.Length < bytes)
                _frame = new byte[bytes];
            WriteLittleEndian(chunk.Span, _frame);
            await _socket.SendBinaryAsync(new ArraySegment<byte>(_frame, 0, bytes), ct);
            _pacer.OnSent(seconds, _clock.Now);
        }

        static void WriteLittleEndian(ReadOnlySpan<short> samples, byte[] destination)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                var s = samples[i];
                destination[2 * i] = (byte)s;
                destination[2 * i + 1] = (byte)(s >> 8);
            }
        }
    }
}
