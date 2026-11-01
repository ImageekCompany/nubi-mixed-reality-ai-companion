using System;
using System.Buffers;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// Fallback <see cref="ISpeechToText"/> (ADR-0005, for when the realtime socket is unusable): buffers the whole
    /// utterance, posts it as WAV to <c>/v1/asr/transcribe</c> when the source completes, and yields the one Final.
    /// No partials. It asks for a JSON response rather than SSE: the audio is complete before the request starts, so
    /// streamed partials could not reach the user any earlier than the final does.
    /// </summary>
    public sealed class MuseFileSpeechToText : ISpeechToText
    {
        readonly IAsrFileClient _client;
        readonly ISecretsProvider _secrets;
        readonly IApiUsageMeter _meter;
        readonly IClock _clock;
        readonly MuseAsrOptions _options;

        public MuseFileSpeechToText(IAsrFileClient client, ISecretsProvider secrets, IApiUsageMeter meter, IClock clock,
                                    MuseAsrOptions options)
        {
            _client = client;
            _secrets = secrets;
            _meter = meter;
            _clock = clock;
            _options = options;
        }

        public IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options,
                                                                   CancellationToken ct) =>
            AsrSessionStream.Start(_secrets, (key, events, token) => RunAsync(audio, options, key, events, token), ct);

        async UniTask RunAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options, string key,
                               ChannelWriter<TranscriptEvent> events, CancellationToken ct)
        {
            var (wav, seconds) = await RecordWavAsync(audio, options.SampleRate, ct);
            var boundary = "jolly" + Guid.NewGuid().ToString("N");
            var body = MuseTranscribeRequest.BuildBody(wav, _options.Model, options, boundary);
            var billing = AsrBilling.Reserve(_meter, _options.Model, _options.ReserveUsd);
            try
            {
                var json = await PostWithTimeoutAsync(body, MuseTranscribeRequest.ContentType(boundary), key, ct);
                events.TryWrite(MuseTranscribeRequest.ParseResponse(json));
            }
            finally
            {
                billing.Settle(seconds);
            }
        }

        static async UniTask<(byte[] Wav, double Seconds)> RecordWavAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, int sampleRate,
                                                                          CancellationToken ct)
        {
            var samples = ArrayPool<short>.Shared.Rent(sampleRate * 10);
            try
            {
                var count = 0;
                await foreach (var chunk in audio.WithCancellation(ct))
                {
                    using (chunk)
                    {
                        if (count + chunk.Length > samples.Length)
                            samples = Grow(samples, count + chunk.Length);
                        chunk.Span.CopyTo(new Span<short>(samples, count, chunk.Length));
                        count += chunk.Length;
                    }
                }
                var wav = MuseTranscribeRequest.EncodeWav(new ReadOnlySpan<short>(samples, 0, count), sampleRate);
                return (wav, (double)count / sampleRate);
            }
            finally
            {
                ArrayPool<short>.Shared.Return(samples);
            }
        }

        async UniTask<string> PostWithTimeoutAsync(byte[] body, string contentType, string key, CancellationToken ct)
        {
            using var request = CancellationTokenSource.CreateLinkedTokenSource(ct);
            using var timeout = new ClockTimeout(_clock, _options.FileTimeout, request);
            try
            {
                return await _client.PostAsync(_options.FileEndpoint, body, contentType, key, request.Token);
            }
            catch (OperationCanceledException) when (timeout.Fired && !ct.IsCancellationRequested)
            {
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Timeout, "Transcribe request timed out.");
            }
        }

        static short[] Grow(short[] buffer, int needed)
        {
            var bigger = ArrayPool<short>.Shared.Rent(Math.Max(needed, buffer.Length * 2));
            Array.Copy(buffer, bigger, buffer.Length);
            ArrayPool<short>.Shared.Return(buffer);
            return bigger;
        }
    }
}
