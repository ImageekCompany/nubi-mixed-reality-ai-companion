using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Jolly.Contracts;

namespace Jolly.Providers.MetaApi.Asr
{
    /// <summary>
    /// The shell both ASR adapters share. The session runs from the moment <c>Transcribe</c> is called, not from the
    /// first enumeration (contracts §5 "connects immediately"), so a caller that enumerates only at PTT release still
    /// overlaps the 1.0–2.9 s handshake with the user speaking; events wait in a channel until read. The session ends
    /// on the caller's token or when the reader stops enumerating early. A missing key fails it as Unauthorized.
    /// </summary>
    sealed class AsrSessionStream
    {
        public delegate UniTask Session(string apiKey, ChannelWriter<TranscriptEvent> events, CancellationToken ct);

        readonly Channel<TranscriptEvent> _channel = Channel.CreateSingleConsumerUnbounded<TranscriptEvent>();
        readonly CancellationTokenSource _lifetime;
        bool _ended;

        AsrSessionStream(CancellationToken ct) => _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);

        public static IUniTaskAsyncEnumerable<TranscriptEvent> Start(ISecretsProvider secrets, Session session, CancellationToken ct)
        {
            var stream = new AsrSessionStream(ct);
            ObserveAsync(stream._channel.Reader.Completion).Forget();
            stream.RunAsync(secrets, session).Forget();
            return UniTaskAsyncEnumerable.Create<TranscriptEvent>(stream.ReadAsync);
        }

        async UniTaskVoid RunAsync(ISecretsProvider secrets, Session session)
        {
            try
            {
                if (!secrets.TryGet(SecretKeys.MetaApiKey, out var key))
                    throw new ProviderException(MuseAsrErrors.Provider, ProviderErrorKind.Unauthorized, "META_API_KEY is missing.");
                await session(key, _channel.Writer, _lifetime.Token);
                _channel.Writer.TryComplete();
            }
            catch (Exception e)
            {
                _channel.Writer.TryComplete(e);
            }
            finally
            {
                _ended = true;
                _lifetime.Dispose();
            }
        }

        async UniTask ReadAsync(IAsyncWriter<TranscriptEvent> writer, CancellationToken ct)
        {
            try
            {
                await foreach (var e in _channel.Reader.ReadAllAsync(ct))
                    await writer.YieldAsync(e);
            }
            finally
            {
                if (!_ended)
                    _lifetime.Cancel();
            }
        }

        /// <summary>
        /// A faulted channel's Completion that nobody awaits is published to UniTaskScheduler as unobserved when it is
        /// finalized. The reader already receives the error through <see cref="ReadAsync"/>.
        /// </summary>
        static async UniTaskVoid ObserveAsync(UniTask completion)
        {
            try { await completion; }
            catch (Exception) { }
        }
    }
}
