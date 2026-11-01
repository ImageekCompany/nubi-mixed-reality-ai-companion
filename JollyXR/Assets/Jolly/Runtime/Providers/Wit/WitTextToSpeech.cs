using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Providers.Wit
{
    /// <summary>
    /// Wit.ai <c>/synthesize</c> as raw streamed PCM (ADR-0006). Each enumeration sends one request; chunks are yielded
    /// on the main thread as bytes arrive. No retry: a sentence that fails is skipped by the SpeechQueue.
    /// </summary>
    public sealed class WitTextToSpeech : ITextToSpeech
    {
        public static readonly TimeSpan DefaultFirstChunkTimeout = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(3);
        /// <summary>Never played; short, so the warm-up costs one small response.</summary>
        public const string WarmUpText = "Hi.";

        readonly ISecretsProvider _secrets;
        readonly IClock _clock;
        readonly TimeSpan _firstChunkTimeout;
        readonly TimeSpan _idleTimeout;

        public WitTextToSpeech(ISecretsProvider secrets, IClock clock, TimeSpan? firstChunkTimeout = null, TimeSpan? idleTimeout = null)
        {
            _secrets = secrets;
            _clock = clock;
            _firstChunkTimeout = firstChunkTimeout ?? DefaultFirstChunkTimeout;
            _idleTimeout = idleTimeout ?? DefaultIdleTimeout;
        }

        public int SampleRate => WitProtocol.SampleRate;
        public int MaxCharacters => 240;

        /// <summary>Our Wit app lists only en_US, en_CA and en_GB voices (live /voices, 2026-11-01).</summary>
        public bool Supports(string languageTag)
            => languageTag != null
               && languageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase)
               && (languageTag.Length == 2 || languageTag[2] == '-' || languageTag[2] == '_');

        public IUniTaskAsyncEnumerable<PcmChunk> Synthesize(string text, VoiceSpec voice, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(text) || text.Length > MaxCharacters)
                throw new ArgumentException($"Text must be 1..{MaxCharacters} characters; callers split sentences.", nameof(text));

            var body = WitProtocol.SynthesizeBody(text, voice);
            var request = UniTaskAsyncEnumerable.Create<PcmChunk>((writer, token) => Stream(body, writer, token, ct));
            return PcmStreamTimeout.Apply(request, _clock, _firstChunkTimeout, _idleTimeout, WitProtocol.Provider);
        }

        /// <summary>
        /// Synthesizes <see cref="WarmUpText"/> in <paramref name="voice"/> through the same streaming request a sentence
        /// uses and discards the audio (Wit TTS is free), so the first real sentence finds everything warm. A
        /// <c>GET /voices</c> warm-up (ADR-0023) opened the connection but not the synthesis path: from the dev Mac the
        /// first <c>/synthesize</c> after it took p50 143 ms against 88 ms after a synthesized warm-up and 83 ms later,
        /// on the same kept-alive connection; on the Quest 472–543 ms against 99–278 ms. Returns the HTTP status (200),
        /// or 0 without a token or a response. Throws only on cancellation.
        /// </summary>
        public async UniTask<long> WarmUp(VoiceSpec voice, CancellationToken ct)
        {
            if (!_secrets.TryGet(SecretKeys.WitClientToken, out _))
                return 0;
            try
            {
                await foreach (var chunk in Synthesize(WarmUpText, voice, ct).WithCancellation(ct))
                    chunk.Dispose();
                return 200;
            }
            catch (ProviderException e)
            {
                return e.HttpStatus;
            }
        }

        async UniTask Stream(string body, IAsyncWriter<PcmChunk> writer, CancellationToken enumerationCt, CancellationToken callerCt)
        {
            if (!_secrets.TryGet(SecretKeys.WitClientToken, out var token))
                throw new ProviderException(WitProtocol.Provider, ProviderErrorKind.Unauthorized, "WIT_AI_CLIENT_TOKEN missing");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(enumerationCt, callerCt);
            await WitSynthesisRequest.Run(token, body, writer, cts.Token);
        }
    }
}
