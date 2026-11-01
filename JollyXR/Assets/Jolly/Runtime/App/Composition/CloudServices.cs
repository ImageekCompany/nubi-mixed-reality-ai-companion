using System;
using Jolly.App.Adapters;
using Jolly.Contracts;
using Jolly.Core.Connectivity;
using Jolly.Net.Retry;
using Jolly.Providers.MetaApi.Asr;
using Jolly.Providers.MetaApi.Chat;
using Jolly.Providers.MetaApi.Common;
using Jolly.Providers.MetaApi.Health;
using Jolly.Providers.MetaApi.Vision;
using Jolly.Providers.Wit;

namespace Jolly.App.Composition
{
    /// <summary>
    /// Every cloud adapter (Meta Model API, Muse ASR, Wit TTS) behind its contract, each wrapped so its outcome reaches the
    /// connectivity monitor. Model ids come from <see cref="JollyConfig"/> through <see cref="MetaModelResolver"/>.
    /// </summary>
    public sealed class CloudServices
    {
        public ConnectivityMonitor Connectivity { get; private set; }
        /// <summary>Voice turns and vision: 4 s first event, one attempt hedged after 2.5 s (ADR-0038).</summary>
        public IChatModel VoiceChat { get; private set; }
        /// <summary>Summarisation: 10 s first event, 3 attempts.</summary>
        public IChatModel BackgroundChat { get; private set; }
        public ISpeechToText SpeechToText { get; private set; }
        /// <summary>The adapter behind <see cref="SpeechToText"/>, for the launch warm-up only.</summary>
        public MuseRealtimeSpeechToText RealtimeAsr { get; private set; }
        public ITextToSpeech TextToSpeech { get; private set; }
        /// <summary>The adapter behind <see cref="TextToSpeech"/>, for the launch warm-up only; null when an edge replaces it.</summary>
        public WitTextToSpeech Wit { get; private set; }
        public ISegmenter Segmenter { get; private set; }
        public IObjectIdentifier Identifier { get; private set; }

        public static CloudServices Build(Foundation f, JollyConfig c, Edges edges, DisposableStack disposables)
        {
            var probe = new MetaHealthProbe(edges.HttpStatus, f.Secrets, f.Clock, f.Log,
                MetaApiEndpoint.DefaultBaseUrl, TimeSpan.FromSeconds(c.healthProbeSeconds));
            var monitor = disposables.Add(new ConnectivityMonitor(probe, f.Secrets, f.Usage, edges.IsNetworkReachable, f.Clock, f.Log,
                new ConnectivityConfig()));
            probe.Unauthorized += () => monitor.ReportFailure(MetaErrors.Provider, ProviderErrorKind.Unauthorized);

            var endpoint = new MetaApiEndpoint(f.Secrets);
            var sse = new MetaSseClient(edges.Http, new RetryExecutor(f.Clock, f.Random, f.Log), f.Clock);
            var liveModel = MetaModelResolver.Resolve(ModelRoute.LiveUser, c.chatModel);
            var syntheticModel = MetaModelResolver.Resolve(ModelRoute.Synthetic, c.syntheticChatModel);
            IChatModel Chat(float firstEventSeconds, int attempts, float hedgeSeconds) => new ReportingChatModel(
                new ResponsesChatModel(endpoint, sse, f.Clock, f.Usage, new ResponsesChatOptions(liveModel, syntheticModel,
                    TimeSpan.FromSeconds(firstEventSeconds), TimeSpan.FromSeconds(c.llmIdleSeconds), attempts,
                    (decimal)c.chatReservationUsd, TimeSpan.FromSeconds(hedgeSeconds)), f.Log),
                MetaErrors.Provider, monitor);

            var asr = new MuseRealtimeSpeechToText(() => edges.WebSockets(MuseAsrErrors.Provider), f.Secrets, f.Usage,
                f.Clock, f.Log, new MuseAsrOptions(MetaModelResolver.Resolve(ModelRoute.LiveUser, c.asrModel)));
            var wit = edges.TextToSpeech == null
                ? new WitTextToSpeech(f.Secrets, f.Clock, TimeSpan.FromSeconds(c.witFirstChunkSeconds),
                    TimeSpan.FromSeconds(c.witIdleSeconds))
                : null;
            var services = new CloudServices
            {
                Connectivity = monitor,
                VoiceChat = Chat(c.voiceFirstEventSeconds, c.voiceAttempts, c.voiceHedgeSeconds),
                BackgroundChat = Chat(c.backgroundFirstEventSeconds, c.backgroundAttempts, 0f),
                RealtimeAsr = asr,
                SpeechToText = new ReportingSpeechToText(asr, MuseAsrErrors.Provider, monitor),
                Wit = wit,
                TextToSpeech = new ReportingTextToSpeech(edges.TextToSpeech ?? wit, WitProtocol.Provider, monitor),
                Segmenter = new ReportingSegmenter(new SamSegmenter(endpoint, sse, f.Usage, f.Clock, f.Log,
                        new SamOptions(MetaModelResolver.Resolve(ModelRoute.LiveUser, c.samModel), PriceOf(c, c.samModel),
                            TimeSpan.FromSeconds(c.samSeconds), c.samAttempts)),
                    MetaErrors.Provider, monitor),
            };
            services.Identifier = new SparkObjectIdentifier(services.VoiceChat, f.Clock, f.Log,
                new SparkIdentifierOptions(TimeSpan.FromSeconds(c.visionSeconds), c.visionMaxOutputTokens, f.SafetyIdentifier));
            return services;
        }

        static decimal PriceOf(JollyConfig c, string model)
        {
            foreach (var p in c.prices)
            {
                if (p.model == model)
                    return (decimal)p.perImage;
            }
            return 0m;
        }
    }
}
