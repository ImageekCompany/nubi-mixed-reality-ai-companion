using System.Collections.Generic;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// The walkable disc used while the room has no Space Setup (ADR-0024): a polygon centred on the user's floor
    /// position, moved when the user walks more than the recentre distance away. It is small on purpose: the floor right
    /// around a standing user is the one place known to be clear of furniture.
    /// </summary>
    public sealed class FallbackFloorArea
    {
        const int MinSegments = 3;

        readonly List<Vector2> _outline = new();
        readonly float _recenterDistance;

        public FallbackFloorArea(float radius, int segments, float recenterDistance)
        {
            _recenterDistance = recenterDistance;
            BuildOutline(radius, segments, _outline);
        }

        /// <summary>Offsets from <see cref="Center"/> on the floor plane (x, y → world x, z).</summary>
        public IReadOnlyList<Vector2> Outline => _outline;
        public bool IsPlaced { get; private set; }
        /// <summary>Floor-plane position; y is 0 (the floor height is the XR side's to know).</summary>
        public Vector3 Center { get; private set; }

        /// <summary>True when the disc must be built around <paramref name="user"/>: not placed yet, or left behind.</summary>
        public bool NeedsPlacement(Vector3 user)
        {
            if (!IsPlaced)
                return true;
            var delta = new Vector2(user.x - Center.x, user.z - Center.z);
            return delta.sqrMagnitude > _recenterDistance * _recenterDistance;
        }

        public void Place(Vector3 user)
        {
            Center = new Vector3(user.x, 0f, user.z);
            IsPlaced = true;
        }

        public void Clear() => IsPlaced = false;

        /// <summary>A regular polygon of at least three sides whose corners lie on the circle, counter-clockwise.</summary>
        public static void BuildOutline(float radius, int segments, List<Vector2> into)
        {
            into.Clear();
            segments = Mathf.Max(MinSegments, segments);
            for (var i = 0; i < segments; i++)
            {
                var angle = 2f * Mathf.PI * i / segments;
                into.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }
}
