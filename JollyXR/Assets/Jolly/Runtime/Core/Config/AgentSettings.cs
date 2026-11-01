using System;

namespace Jolly.Core.Config
{
    /// <summary>Agent loop limits of architecture §7; defaults are the documented values.</summary>
    public sealed class AgentSettings
    {
        public int MaxRounds { get; set; } = 4;
        public int SummaryContextChars { get; set; } = 1200;
        public int SceneContextChars { get; set; } = 800;
        public int RoomContextChars { get; set; } = 300;
        /// <summary>
        /// A model round with no first event after this long raises <c>AgentEventKind.Delayed</c> (a hold
        /// line); zero turns it off. Request → first event took 0.3–1.4 s in 52 device turns (selftest-report.md).
        /// </summary>
        public TimeSpan HoldLineAfter { get; set; } = TimeSpan.FromSeconds(2);
        /// <summary>While still waiting (the retry after a first-event timeout), once more after this long; zero: never.</summary>
        public TimeSpan HoldLineRepeat { get; set; } = TimeSpan.FromSeconds(4);
        /// <summary>From <c>InstallIdentity.LoadOrCreateSafetyIdentifier</c>; sent as the Responses <c>safety_identifier</c>.</summary>
        public string SafetyIdentifier { get; set; }
    }
}
