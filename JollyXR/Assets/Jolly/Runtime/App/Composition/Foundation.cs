using System;
using System.Collections.Generic;
using Jolly.App.Platform;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Diagnostics;
using Jolly.Core.Secrets;
using Jolly.Core.Usage;

namespace Jolly.App.Composition
{
    /// <summary>The services everything else depends on: time, randomness, logging, secrets, files, budget and the
    /// conversation's shared state machine and events. The owner disposes <see cref="Usage"/>.</summary>
    public sealed class Foundation
    {
        Foundation(AppPaths paths, IClock clock, IRandom random, ILog log, ISecretsProvider secrets, string safetyIdentifier,
                   ApiUsageMeter usage)
        {
            Paths = paths;
            Clock = clock;
            Random = random;
            Log = log;
            Secrets = secrets;
            SafetyIdentifier = safetyIdentifier;
            Usage = usage;
            Latency = new LatencyTracker(clock, log);
            Machine = new ConversationStateMachine(log);
        }

        public AppPaths Paths { get; }
        public IClock Clock { get; }
        public IRandom Random { get; }
        public ILog Log { get; }
        public ISecretsProvider Secrets { get; }
        public string SafetyIdentifier { get; }
        public ApiUsageMeter Usage { get; }
        public LatencyTracker Latency { get; }
        public ConversationEvents Events { get; } = new();
        public ConversationStateMachine Machine { get; }

        public static Foundation Build(JollyConfig config, Edges edges)
        {
            var clock = new UnityClock();
            var paths = edges.Paths;
            var secrets = edges.Secrets;
            var log = new RedactingLog(new UnityLog(config.minimumLogLevel), SecretsFactory.ValuesToRedact(secrets));
            var safetyId = InstallIdentity.LoadOrCreateSafetyIdentifier(paths.InstallId, Guid.NewGuid);
            var usage = new ApiUsageMeter(Prices(config), paths.Usage, (decimal)config.hardCapUsd, clock);
            return new Foundation(paths, clock, new SystemRandom(Environment.TickCount), log, secrets, safetyId, usage);
        }

        static PriceTable Prices(JollyConfig config)
        {
            var prices = new Dictionary<string, ModelPrice>(config.prices.Length, StringComparer.Ordinal);
            foreach (var p in config.prices)
            {
                prices[p.model] = new ModelPrice((decimal)p.inputPerMillion, (decimal)p.cachedInputPerMillion,
                    (decimal)p.outputPerMillion, (decimal)p.perImage, (decimal)p.perAudioHour);
            }
            return new PriceTable(prices);
        }
    }
}
