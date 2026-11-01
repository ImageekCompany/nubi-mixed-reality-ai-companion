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
                                    TimeSpan idleTimeout, int maxAttempts, decimal reservationUsd)
        {
            LiveUserModel = liveUserModel;
            SyntheticModel = syntheticModel;
            FirstEventTimeout = firstEventTimeout;
            IdleTimeout = idleTimeout;
            RetryPolicy = new RetryPolicy(maxAttempts);
            ReservationUsd = reservationUsd;
        }

        /// <summary>6 s first event × 2 attempts (≤ 13 s with backoff, under the 20 s Thinking watchdog).</summary>
        public static ResponsesChatOptions VoiceTurn(string liveUserModel, string syntheticModel, decimal reservationUsd) =>
            new(liveUserModel, syntheticModel, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(10), 2, reservationUsd);

        /// <summary>10 s first event × 3 attempts.</summary>
        public static ResponsesChatOptions Background(string liveUserModel, string syntheticModel, decimal reservationUsd) =>
            new(liveUserModel, syntheticModel, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), 3, reservationUsd);

        public string LiveUserModel { get; }
        public string SyntheticModel { get; }
        public TimeSpan FirstEventTimeout { get; }
        public TimeSpan IdleTimeout { get; }
        public RetryPolicy RetryPolicy { get; }
        /// <summary>Reserved in IApiUsageMeter before each request and settled with the actual usage.</summary>
        public decimal ReservationUsd { get; }

        public string ModelFor(ModelRoute route) => route == ModelRoute.LiveUser ? LiveUserModel : SyntheticModel;
    }
}
