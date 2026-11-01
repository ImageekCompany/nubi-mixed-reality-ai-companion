namespace Jolly.Core.Config
{
    /// <summary>Agent loop limits of architecture §7; defaults are the documented values.</summary>
    public sealed class AgentSettings
    {
        public int MaxRounds { get; set; } = 4;
        public int SummaryContextChars { get; set; } = 1200;
        public int SceneContextChars { get; set; } = 800;
        public int RoomContextChars { get; set; } = 300;
        /// <summary>From <c>InstallIdentity.LoadOrCreateSafetyIdentifier</c>; sent as the Responses <c>safety_identifier</c>.</summary>
        public string SafetyIdentifier { get; set; }
    }
}
