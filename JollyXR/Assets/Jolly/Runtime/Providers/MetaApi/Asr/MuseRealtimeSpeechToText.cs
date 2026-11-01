using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Common;
using Jolly.Net.WebSockets;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// <see cref="ISpeechToText"/> over Muse Voice Transcribe realtime (ADR-0005). <see cref="Transcribe"/> connects at
    /// once (<see cref="AsrSessionStream"/>), so a call at PTT press overlaps the 1.0–2.9 s handshake measured on Quest
    /// with the user speaking. Audio buffered in the source meanwhile goes out at once after the ack.
    /// Fails with ProviderException: Unauthorized (no key, rejected key), BudgetExceeded, Timeout (not open in 5 s),
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
            AsrSessionStream.Start(_secrets, (key, events, token) => RunAsync(audio, options, key, events, token), ct);

        async UniTask RunAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, SttOptions options, string key,
                               ChannelWriter<TranscriptEvent> events, CancellationToken ct)
        {
            var model = MetaModelResolver.Resolve(ModelRoute.LiveUser, _options.Model);
            var handshake = MuseAsrHandshake.Build(key, model, options);
            var billing = AsrBilling.Reserve(_meter, model, _options.ReserveUsd);

            using var socket = _connect();
            var session = new MuseRealtimeSession(socket, _clock, _log, _options, options.SampleRate);
            try
            {
                await session.RunAsync(audio, handshake, SessionUri(), events, ct);
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
