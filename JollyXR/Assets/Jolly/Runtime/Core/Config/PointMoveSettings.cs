using System;

namespace Jolly.Core.Config
{
    /// <summary>Point-and-tap moves (ADR-0044); JollyConfig sets <see cref="TapMax"/>.</summary>
    public sealed class PointMoveSettings
    {
        /// <summary>A pinch released sooner, with a target showing, is a tap that moves; held longer it talks.</summary>
        public TimeSpan TapMax { get; set; } = TimeSpan.FromSeconds(0.25);
        public float RayMaxDistance { get; set; } = 8f;
        /// <summary>How far the walkable floor may be from the ray's floor hit for the ring to show there.</summary>
        public float FloorSnapRadius { get; set; } = 0.25f;
        /// <summary>A hit this close in height to the character's feet is floor.</summary>
        public float FloorTolerance { get; set; } = 0.1f;
        /// <summary>A hit this close in height to a surface's top, and this far around its footprint, is that top.</summary>
        public float SurfaceTolerance { get; set; } = 0.12f;
        public float StopDistance { get; set; } = 0.1f;
    }
}
