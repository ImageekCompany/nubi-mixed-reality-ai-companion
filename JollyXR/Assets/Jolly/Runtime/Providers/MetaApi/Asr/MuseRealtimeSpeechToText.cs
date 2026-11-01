using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;
using Jolly.Net.WebSockets;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// <see cref="ISpeechToText"/> over Muse Voice Transcribe realtime (ADR-0005). The session starts when the stream is
    /// first enumerated, so the caller enumerates at PTT press: the 1.0–2.9 s handshake measured on Quest then overlaps
    /// the user speaking. Audio buffered in the source meanwhile goes out at once after the ack.
    /// Fails with ProviderException: Unauthorized (no key, rejected key), BudgetExceeded, Timeout (no ack),
    /// BadRequest, RateLimited, Server, Network or Protocol.
    /// </summary>
    public sealed class MuseRealtimeSpeechToText : ISpeechToText
    {
        readonly Func<IWebSocketConnection> _connect;
        readonly ISecretsProvider _secrets;
        readonly IApiUsageMeter _meter;
        readonly IClock _clock;
        readonly ILog _log;
        readonly MuseAsrOptions _options;

        public MuseRealtimeSpeechToText(Func<IWebSocketConnection> connect, ISecretsProvider secrets, IApiUsageMeter meter,
                                        IClock clock, ILog log, MuseAsrOptions options)
        {
            _connect = connect;
            _secrets = secrets;
            _meter = meter;
            _clock = clock;
            _log = log;
            _options = options;
        }

        public IUniTaskAsyncEnumerable<TranscriptEvent> Transcribe(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options,
                                                                   CancellationToken ct) =>
            UniTaskAsyncEnumerable.Create<TranscriptEvent>((writer, enumerationCt) => RunAsync(audio, options, writer, ct, enumerationCt));

        async UniTask RunAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options, IAsyncWriter<TranscriptEvent> writer,
                               CancellationToken ct, CancellationToken enumerationCt)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, enumerationCt);
            if (!_secrets.TryGet(SecretKeys.MetaApiKey, out var key))
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Unauthorized, "META_API_KEY is missing.");
            var handshake = MuseAsrHandshake.Build(key, _options.Model, options);
            var billing = AsrBilling.Reserve(_meter, _options.Model, _options.ReserveUsd);

            using var socket = _connect();
            var session = new MuseRealtimeSession(socket, _clock, _log, _options, options.SampleRate);
            try
            {
                await session.RunAsync(audio, handshake, SessionUri(), writer, linked.Token);
            }
            finally
            {
                billing.Settle(session.SentSeconds);
            }
        }

        /// <summary>The optional sessionId is echoed in the ack and in server errors, which ties logs to a session.</summary>
        Uri SessionUri() =>
            new(_options.RealtimeEndpoint.AbsoluteUri + "?sessionId=jolly-" + Guid.NewGuid().ToString("N").Substring(0, 12));
    }
}
