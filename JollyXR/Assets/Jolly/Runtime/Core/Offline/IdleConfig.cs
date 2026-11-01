using System;

namespace Jolly.Core.Offline
{
    /// <summary>IdleScheduler tuning (architecture §5.4). Weights are relative; a zero weight disables a behaviour.</summary>
    public sealed class IdleConfig
    {
        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(8);
        public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(20);
        public float LookAroundWeight { get; set; } = 3f;
        public float StretchWeight { get; set; } = 1f;
        public float WanderWeight { get; set; } = 2f;
        public float GlanceWeight { get; set; } = 3f;
        public float WanderMinDistance { get; set; } = 0.5f;
        public float WanderRadius { get; set; } = 1.5f;
        public float WanderStopDistance { get; set; } = 0.1f;
        public TimeSpan GlanceDuration { get; set; } = TimeSpan.FromSeconds(2.5);
    }
}
