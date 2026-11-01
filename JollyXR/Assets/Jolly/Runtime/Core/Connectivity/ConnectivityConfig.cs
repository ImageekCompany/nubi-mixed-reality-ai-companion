using System;

namespace Jolly.Core.Connectivity
{
    public sealed class ConnectivityConfig
    {
        /// <summary>How often reachability is polled and a due probe is started. Bounds offline detection after a Wi-Fi drop.</summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan ProbeBackoffMin { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan ProbeBackoffMax { get; set; } = TimeSpan.FromSeconds(30);
        public int FailuresToOffline { get; set; } = 2;
    }
}
