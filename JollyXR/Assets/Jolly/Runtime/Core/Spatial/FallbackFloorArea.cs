using System.Collections.Generic;
using UnityEngine;

namespace Jolly.Core.Spatial
{
    /// <summary>
    /// The walkable area used while the room has no Space Setup (ADR-0024): a disc centred on the user's floor position,
    /// stretched into the convex hull of that disc and a smaller one around Nubi, so the rebuilt NavMesh always still
    /// holds Nubi and the user can walk away without stranding it. It is small on purpose: the floor right around a
    /// standing user is the one place known to be clear of furniture, and the strip towards Nubi is the floor it just
    /// walked or stood on. Past the maximum span the strip would cross unknown floor, so the area is the user's disc
    /// alone and Nubi has to be brought back into it (<see cref="ReturnPoint"/>).
    /// </summary>
    public sealed class FallbackFloorArea
    {
        const int MinSegments = 3;
        /// <summary>How deep inside the outline Nubi must stand to count as on it: the NavMesh is eroded by the 0.15 m
        /// agent radius, plus slack for the polygon's flat sides.</summary>
        public const float CharacterMargin = 0.25f;
        /// <summary>Where a returning Nubi lands, from the user: about where come_here puts it.</summary>
        const float ReturnDistance = 1f;

        static readonly IComparer<Vector2> ByXThenY = Comparer<Vector2>.Create((a, b) =>
            a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        readonly List<Vector2> _outline = new();
        readonly List<Vector2> _userDisc = new();
        readonly List<Vector2> _characterDisc = new();
        readonly List<Vector2> _points = new();
        readonly float _radius;
        readonly float _characterRadius;
        readonly float _recenterDistance;
        readonly float _maxSpan;

        /// <param name="radius">Disc around the user.</param>
        /// <param name="characterRadius">Disc around Nubi when it stands outside the user's disc.</param>
        /// <param name="maxSpan">Farthest user-to-Nubi floor distance the area stretches over.</param>
        public FallbackFloorArea(float radius, float characterRadius, int segments, float recenterDistance, float maxSpan)
        {
            _radius = radius;
            _characterRadius = Mathf.Min(characterRadius, radius);
            _recenterDistance = recenterDistance;
            _maxSpan = maxSpan;
            BuildOutline(radius, segments, _userDisc);
            BuildOutline(_characterRadius, segments, _characterDisc);
        }

        /// <summary>Offsets from <see cref="Center"/> on the floor plane (x, y → world x, z), counter-clockwise, convex.</summary>
        public IReadOnlyList<Vector2> Outline => _outline;
        public bool IsPlaced { get; private set; }
        /// <summary>The user's floor position when placed; y is 0 (the floor height is the XR side's to know).</summary>
        public Vector3 Center { get; private set; }

        /// <summary>True when the area must be rebuilt: not placed yet, the user left the recentre distance, or Nubi
        /// stands outside it.</summary>
        public bool NeedsPlacement(Vector3 user, Vector3 character)
        {
            if (!IsPlaced)
                return true;
            var delta = new Vector2(user.x - Center.x, user.z - Center.z);
            return delta.sqrMagnitude > _recenterDistance * _recenterDistance || !Contains(character, CharacterMargin);
        }

        /// <summary>Builds the area around the user and Nubi. False if Nubi is farther than the maximum span: the area
        /// is then the user's disc alone, and Nubi must move to <see cref="ReturnPoint"/>.</summary>
        public bool Place(Vector3 user, Vector3 character)
        {
            Center = new Vector3(user.x, 0f, user.z);
            IsPlaced = true;
            var offset = Flat(character);
            var span = offset.magnitude;
            var reachable = span <= _maxSpan;

            _points.Clear();
            _points.AddRange(_userDisc);
            if (reachable && span + _characterRadius > _radius)
                foreach (var corner in _characterDisc)
                    _points.Add(corner + offset);
            ConvexHull(_points, _outline);
            return reachable;
        }

        public void Clear() => IsPlaced = false;

        /// <summary>True if the floor position of <paramref name="world"/> is at least <paramref name="margin"/> inside the outline.</summary>
        public bool Contains(Vector3 world, float margin)
        {
            if (!IsPlaced)
                return false;
            var p = Flat(world);
            for (var i = 0; i < _outline.Count; i++)
            {
                var a = _outline[i];
                var edge = _outline[(i + 1) % _outline.Count] - a;
                // Left of a counter-clockwise edge is inside; the cross product over the edge length is the distance.
                if (Cross(edge, p - a) < margin * edge.magnitude)
                    return false;
            }
            return true;
        }

        /// <summary>The floor point (y 0) inside the user's disc, on the side Nubi is, where a stranded Nubi goes back to.</summary>
        public Vector3 ReturnPoint(Vector3 character)
        {
            var offset = Flat(character);
            var direction = offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector2.up;
            var p = direction * Mathf.Min(ReturnDistance, _radius - CharacterMargin);
            return new Vector3(Center.x + p.x, 0f, Center.z + p.y);
        }

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

        /// <summary>Andrew's monotone chain: the counter-clockwise hull without collinear corners. Sorts points.</summary>
        static void ConvexHull(List<Vector2> points, List<Vector2> into)
        {
            into.Clear();
            points.Sort(ByXThenY);
            for (var i = 0; i < points.Count; i++)
                Push(into, points[i], 2);
            var lower = into.Count + 1;
            for (var i = points.Count - 2; i >= 0; i--)
                Push(into, points[i], lower);
            into.RemoveAt(into.Count - 1);
        }

        static void Push(List<Vector2> hull, Vector2 p, int floor)
        {
            while (hull.Count >= floor && Cross(hull[^1] - hull[^2], p - hull[^2]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }

        Vector2 Flat(Vector3 world) => new(world.x - Center.x, world.z - Center.z);

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
