using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Net.WebSockets;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// One push-to-talk WebSocket session: connect → handshake → ack, all under one clock timeout (a dead route stalls
    /// the connect itself for the OS timeout, tens of seconds) → uplink and downlink run
    /// concurrently → the first final ends it. An uplink failure cancels the downlink and is rethrown in its place,
    /// so the caller always sees the root cause rather than the cancellation it triggered.
    /// </summary>
    sealed class MuseRealtimeSession
    {
        readonly IWebSocketConnection _socket;
        readonly IClock _clock;
        readonly ILog _log;
        readonly MuseAsrOptions _options;
        readonly MuseAudioUplink _uplink;
        ExceptionDispatchInfo _uplinkFailure;
        string _sessionId;

        public MuseRealtimeSession(IWebSocketConnection socket, IClock clock, ILog log, MuseAsrOptions options, int sampleRate)
        {
            _socket = socket;
            _clock = clock;
            _log = log;
            _options = options;
            _uplink = new MuseAudioUplink(socket, clock, sampleRate, options.MaxAheadSeconds);
        }

        public double SentSeconds => _uplink.SentSeconds;

        public async UniTask RunAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, string handshake, Uri uri,
                                      ChannelWriter<TranscriptEvent> events, CancellationToken ct)
        {
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var started = _clock.Now;
            try
            {
                await OpenAsync(uri, handshake, session);
                Log(LogLevel.Info, "evt=asr.ack ms={0:F0} session={1}", (_clock.Now - started) * 1000, _sessionId);

                SendAsync(audio, session).Forget();
                await ReceiveUntilFinalAsync(events, session.Token);
            }
            catch (OperationCanceledException) when (_uplinkFailure != null)
            {
                _uplinkFailure.Throw();
            }
            finally
            {
                session.Cancel();
            }
        }

        async UniTask OpenAsync(Uri uri, string handshake, CancellationTokenSource session)
        {
            using var open = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            using var timeout = new ClockTimeout(_clock, _options.AckTimeout, open);
            try
            {
                await _socket.ConnectAsync(uri, open.Token);
                await _socket.SendTextAsync(handshake, open.Token);
                while (true)
                {
                    var e = await ReceiveEventAsync(open.Token);
                    if (e.Kind == MuseAsrEventKind.Ack)
                    {
                        _sessionId = e.SessionId;
                        return;
                    }
                    if (e.Kind == MuseAsrEventKind.Transcript)
                        throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Protocol, "Transcript before the ack.");
                }
            }
            catch (OperationCanceledException) when (timeout.Fired && !session.IsCancellationRequested)
            {
                throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Timeout,
                    $"ASR session not acknowledged within {_options.AckTimeout.TotalSeconds:F0} s of connecting.");
            }
        }

        async UniTask ReceiveUntilFinalAsync(ChannelWriter<TranscriptEvent> events, CancellationToken ct)
        {
            string lastPartial = null;
            while (true)
            {
                var e = await ReceiveEventAsync(ct);
                if (e.Kind != MuseAsrEventKind.Transcript)
                    continue;

                var text = TranscriptText.Normalize(e.Text);
                if (e.Final)
                {
                    Log(LogLevel.Info, "evt=asr.final ms_after_end={0:F0} audio_ms={1:F0}",
                        (_clock.Now - _uplink.EndStreamTime) * 1000, e.AudioMs);
                    events.TryWrite(new TranscriptEvent(TranscriptKind.Final, text, e.AudioMs));
                    return;
                }
                if (text == lastPartial)
                    continue;
                lastPartial = text;
                events.TryWrite(new TranscriptEvent(TranscriptKind.Partial, text, e.AudioMs));
            }
        }

        /// <summary>Next frame as an event; error frames and closes become exceptions.</summary>
        async UniTask<MuseAsrEvent> ReceiveEventAsync(CancellationToken ct)
        {
            while (true)
            {
                var message = await _socket.ReceiveAsync(ct);
                if (message.Kind == WebSocketMessageKind.Close)
                    throw MuseAsrErrors.FromClose(message.CloseStatus, message.CloseDescription, _sessionId);
                if (message.Kind != WebSocketMessageKind.Text)
                    continue;

                var e = MuseAsrEventParser.Parse(message.Text);
                if (e.Kind == MuseAsrEventKind.Error)
                    throw MuseAsrErrors.FromEvent(e);
                return e;
            }
        }

        async UniTaskVoid SendAsync(IUniTaskAsyncEnumerable<PcmChunk> audio, CancellationTokenSource session)
        {
            try
            {
                await _uplink.RunAsync(audio, session.Token);
            }
            catch (Exception e) when (!session.IsCancellationRequested)
            {
                _uplinkFailure = ExceptionDispatchInfo.Capture(e);
                session.Cancel();
            }
            catch (Exception)
            {
                // The session already ended (final, failure or caller cancel); a late send error has nobody to tell.
            }
        }

        void Log(LogLevel level, string format, double value, object detail)
        {
            if (_log.IsEnabled(level))
                _log.Write(level, "asr", string.Format(CultureInfo.InvariantCulture, format, value, detail));
        }
    }
}
