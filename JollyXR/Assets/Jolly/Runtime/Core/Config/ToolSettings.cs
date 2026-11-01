using System;

namespace Jolly.Core.Config
{
    /// <summary>Locomotion tool distances (architecture §5.5).</summary>
    public sealed class LocomotionSettings
    {
        public float WalkStopDistance { get; set; } = 0.4f;
        public float ComeHereDistance { get; set; } = 1.0f;
        public float ComeHereStopDistance { get; set; } = 0.15f;
        public float SampleRadius { get; set; } = 1.0f;
        public float FollowMinDistance { get; set; } = 1.0f;
        public float FollowMaxDistance { get; set; } = 1.5f;
        public float LookRayMaxDistance { get; set; } = 5f;
    }

    /// <summary>Capture, SAM and vision numbers of architecture §5.3 (one set, shared with JollyConfig).</summary>
    public sealed class VisionSettings
    {
        public int MaxSide { get; set; } = 640;
        public int JpegQuality { get; set; } = 80;
        public TimeSpan PreCaptureMaxAge { get; set; } = TimeSpan.FromSeconds(3);
        public TimeSpan SamTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan VisionTimeout { get; set; } = TimeSpan.FromSeconds(8);
        public float PointingMaxDistance { get; set; } = 2f;
        public float RaycastMaxDistance { get; set; } = 5f;
        public float FallbackDistance { get; set; } = 0.6f;
        public float MaxMaskCoverage { get; set; } = 0.5f;
        public int MaxInstances { get; set; } = 16;
        public int InteriorSamples { get; set; } = 8;
    }
}
