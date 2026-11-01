namespace Jolly.Core.Config
{
    /// <summary>Memory caps of architecture §8. JollyConfig (WP9) may override; the defaults are the documented values.</summary>
    public sealed class MemoryLimits
    {
        public int RecentItems { get; set; } = 16;
        public int RecentChars { get; set; } = 6000;
        public int SummaryChars { get; set; } = 1200;
        public int SummaryOutputTokens { get; set; } = 300;
        public int SceneObjects { get; set; } = 64;
        public int Places { get; set; } = 32;
        public int ObjectSummaryChars { get; set; } = 140;
        public int LabelChars { get; set; } = 40;
    }
}
