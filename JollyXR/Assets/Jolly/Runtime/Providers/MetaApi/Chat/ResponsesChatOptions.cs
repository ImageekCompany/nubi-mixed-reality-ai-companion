using System;
using Jolly.Contracts;
using Jolly.Net.Retry;

namespace Jolly.Providers.MetaApi.Chat
{
    /// <summary>
    /// Per-instance settings. The composition root builds one model for live voice turns and one for background calls
    /// (summarisation), because their timeout budgets differ (architecture §7, §11). Model ids come from JollyConfig.
    /// </summary>
    public sealed class ResponsesChatOptions
    {
        public ResponsesChatOptions(string liveUserModel, string syntheticModel, TimeSpan firstEventTimeout,
                                    TimeSpan idleTimeout, int maxAttempts, decimal reservationUsd, TimeSpan hedgeAfter = default)
        {
            LiveUserModel = liveUserModel;
            SyntheticModel = syntheticModel;
            FirstEventTimeout = firstEventTimeout;
            IdleTimeout = idleTimeout;
            RetryPolicy = new RetryPolicy(maxAttempts);
            ReservationUsd = reservationUsd;
            HedgeAfter = hedgeAfter;
        }

        /// <summary>4 s first event, one attempt, hedged after 2.5 s (ADR-0038): a silent turn fails at 4 s.</summary>
        public static ResponsesChatOptions VoiceTurn(string liveUserModel, string syntheticModel, decimal reservationUsd) =>
            new(liveUserModel, syntheticModel, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(10), 1, reservationUsd,
                TimeSpan.FromSeconds(2.5));

        /// <summary>10 s first event × 3 attempts.</summary>
        public static ResponsesChatOptions Background(string liveUserModel, string syntheticModel, decimal reservationUsd) =>
            new(liveUserModel, syntheticModel, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), 3, reservationUsd);

        public string LiveUserModel { get; }
        public string SyntheticModel { get; }
        public TimeSpan FirstEventTimeout { get; }
        public TimeSpan IdleTimeout { get; }
        /// <summary>Applies to each leg of a hedge.</summary>
        public RetryPolicy RetryPolicy { get; }
        /// <summary>
        /// Without a first event this long after the request, an identical second request races it
        /// (<see cref="FirstEventHedge"/>); zero turns hedging off.
        /// </summary>
        public TimeSpan HedgeAfter { get; }
        /// <summary>Reserved in IApiUsageMeter before each request and settled with the actual usage.</summary>
        public decimal ReservationUsd { get; }

        public string ModelFor(ModelRoute route) => route == ModelRoute.LiveUser ? LiveUserModel : SyntheticModel;
    }
}
