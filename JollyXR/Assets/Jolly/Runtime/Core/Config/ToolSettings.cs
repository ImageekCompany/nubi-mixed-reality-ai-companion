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
        /// <summary>How far a place the user shows can be: another room's doorway is further than anything looked at.</summary>
        public float ShownPlaceRayMaxDistance { get; set; } = 10f;
        /// <summary>Where a shown place lands when the ray hits nothing: depth and the room scan end at the scanned room.</summary>
        public float ShownPlaceFallbackDistance { get; set; } = 3f;
        /// <summary>show_place stops this far short of the place, so the arm still points at something ahead, m.</summary>
        public float ShowStandOff { get; set; } = 1f;
        /// <summary>show_place stays put when this much closer than <see cref="ShowStandOff"/> would make the walk a shuffle, m.</summary>
        public float ShowMinWalk { get; set; } = 0.3f;
        /// <summary>The Point clip's arm, degrees to the character's right of its body, measured on the played clip
        /// (PointToPlaceTests): POINT_DIR_R in clips.py alone says 12.5, before the torso's part.</summary>
        public float PointArmYaw { get; set; } = 21f;
        /// <summary>The pointing shoulder's distance to the right of the body's axis, m: at the stand-off the arm's line
        /// misses by about 13 degrees if the body is aimed as if the arm started at its centre.</summary>
        public float PointShoulderOffset { get; set; } = 0.23f;
        /// <summary>A turn smaller than this before pointing would read as a twitch, degrees.</summary>
        public float PointMinTurn { get; set; } = 5f;
        /// <summary>Gap between a furniture footprint and where Nubi stops beside it: the 0.15 m NavMesh agent radius
        /// plus a margin, since the scanned box edge is only as exact as Space Setup.</summary>
        public float SurfaceClearance { get; set; } = 0.25f;
        public float SurfaceStopDistance { get; set; } = 0.1f;
        /// <summary>actions.move_relative "closer": where the character stops in front of the user, m.</summary>
        public float CloserDistance { get; set; } = 0.7f;
        /// <summary>No relative move ends nearer the user than this, the same 0.5 m as a tap target (PointMoveSettings).</summary>
        public float UserMinDistance { get; set; } = 0.5f;
        /// <summary>A relative target snaps to walkable floor only this far, so "behind me" stays behind the user.</summary>
        public float RelativeSampleRadius { get; set; } = 0.5f;
        /// <summary>A relative move whose straight line passes nearer the user than this walks round them instead, m.</summary>
        public float UserPassClearance { get; set; } = 0.6f;
        /// <summary>How far from the user the walk round them turns, m. Both legs keep over 0.6 m from the user when the
        /// character starts 1 m or more away; the locomotion counts the turn reached 0.15 m short of it, since turning
        /// further short pulls the second leg in towards the user.</summary>
        public float DetourRadius { get; set; } = 1.0f;
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
        /// <summary>A frame darker than this (mean luma 0–255) gets no SAM or Spark call; JollyConfig.visionMinMeanLuma.</summary>
        public float MinMeanLuma { get; set; } = 25f;
        /// <summary>The ids the vision path reports on ModelCallCompleted (JollyConfig.samModel, chatModel).</summary>
        public string SegmenterModel { get; set; } = "sam-3.1";
        public string VisionModel { get; set; } = "muse-spark-1.3";
    }
}
