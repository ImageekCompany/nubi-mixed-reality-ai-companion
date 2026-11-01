using System.Collections.Generic;
using UnityEngine;

namespace Jolly.XR.Navigation
{
    /// <summary>
    /// Ear-clipping triangulation of a simple polygon (an AR floor boundary in plane space). Room floors from Space
    /// Setup can be concave (L-shaped rooms), so a centre fan would put walkable area outside the room. O(n²), run only
    /// when a floor boundary changes.
    /// </summary>
    public static class PolygonTriangulator
    {
        const float Epsilon = 1e-7f;

        /// <summary>Clears triangles and fills it with indices into polygon (either winding). Triangles are wound so
        /// that, with plane-space (x, y) mapped to world (x, z), Unity sees their front faces from +Y.</summary>
        public static bool Triangulate(IReadOnlyList<Vector2> polygon, List<int> triangles)
        {
            triangles.Clear();
            var n = polygon.Count;
            if (n < 3 || Mathf.Abs(SignedArea(polygon)) < Epsilon)
                return false;

            var remaining = new List<int>(n);
            var counterClockwise = SignedArea(polygon) > 0f;
            for (var i = 0; i < n; i++)
                remaining.Add(counterClockwise ? i : n - 1 - i);

            while (remaining.Count > 3)
            {
                if (!TryClipEar(polygon, remaining, triangles))
                {
                    triangles.Clear();
                    return false;
                }
            }
            Emit(remaining[0], remaining[1], remaining[2], triangles);
            return true;
        }

        static bool TryClipEar(IReadOnlyList<Vector2> p, List<int> remaining, List<int> triangles)
        {
            var count = remaining.Count;
            for (var i = 0; i < count; i++)
            {
                int a = remaining[(i + count - 1) % count], b = remaining[i], c = remaining[(i + 1) % count];
                if (Cross(p[a], p[b], p[c]) <= Epsilon || AnyInside(p, remaining, a, b, c))
                    continue;
                Emit(a, b, c, triangles);
                remaining.RemoveAt(i);
                return true;
            }
            return false;
        }

        static bool AnyInside(IReadOnlyList<Vector2> p, List<int> remaining, int a, int b, int c)
        {
            foreach (var v in remaining)
            {
                if (v != a && v != b && v != c
                    && Cross(p[a], p[b], p[v]) >= 0f && Cross(p[b], p[c], p[v]) >= 0f && Cross(p[c], p[a], p[v]) >= 0f)
                    return true;
            }
            return false;
        }

        // Counter-clockwise in plane space reads clockwise from above once y becomes world z: emit reversed.
        static void Emit(int a, int b, int c, List<int> triangles)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        static float SignedArea(IReadOnlyList<Vector2> p)
        {
            var area = 0f;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                area += (p[j].x * p[i].y) - (p[i].x * p[j].y);
            return area * 0.5f;
        }
    }
}
