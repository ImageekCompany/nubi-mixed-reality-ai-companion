using System;

namespace Jolly.Core.Config
{
    /// <summary>The room-scan flow and the fallback floor (ADR-0024); JollyConfig sets the shipped values.</summary>
    public sealed class RoomSettings
    {
        /// <summary>How long after USE_SCENE is granted (or the app resumes) a missing floor plane means "no Space Setup".</summary>
        public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromSeconds(8);
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(0.5);
        public bool FallbackFloor { get; set; } = true;
        public float FallbackRadius { get; set; } = 1.5f;
        public int FallbackSegments { get; set; } = 24;
        /// <summary>The disc is rebuilt around the user once they are this far from its centre.</summary>
        public float FallbackRecenterDistance { get; set; } = 0.5f;
        /// <summary>Speak the Space Setup offer once per session when the room turns out unscanned.</summary>
        public bool OfferScan { get; set; } = true;
        /// <summary>A queued Space Setup waits this long for Nubi to fall silent, then opens anyway.</summary>
        public TimeSpan ScanRequestMaxWait { get; set; } = TimeSpan.FromSeconds(10);
    }
}
