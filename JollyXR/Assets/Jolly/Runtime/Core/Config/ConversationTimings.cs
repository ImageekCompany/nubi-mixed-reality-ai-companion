using System;

namespace Jolly.Core.Config
{
    /// <summary>Watchdog limits of architecture §6 and the capture rate for ASR.</summary>
    public sealed class ConversationTimings
    {
        /// <summary>Counted from PTT release: no final within this time resets to Idle.</summary>
        public TimeSpan Listening { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan Thinking { get; set; } = TimeSpan.FromSeconds(20);
        public TimeSpan Acting { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan Speaking { get; set; } = TimeSpan.FromSeconds(60);
        public int CaptureSampleRate { get; set; } = 24000;
        /// <summary>Plays an Acknowledge clip at PTT release so the user hears a reply within ~150 ms (should tier).</summary>
        public bool AcknowledgeOnRelease { get; set; } = true;
    }
}
