using System.Collections.Generic;
using Jolly.XR.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class PolygonTriangulatorTests
    {
        static float TriangulatedArea(IReadOnlyList<Vector2> polygon, List<int> triangles)
        {
            var area = 0f;
            for (var i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = polygon[triangles[i]], b = polygon[triangles[i + 1]], c = polygon[triangles[i + 2]];
                area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            }
            return area;
        }

        [Test]
        public void Triangulate_Square_TwoTriangles()
        {
            var square = new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };
            var triangles = new List<int>();

            Assert.That(PolygonTriangulator.Triangulate(square, triangles), Is.True);

            Assert.That(triangles, Has.Count.EqualTo(6));
            Assert.That(TriangulatedArea(square, triangles), Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void Triangulate_ConcaveLShapedRoom_CoversExactlyTheFloor()
        {
            // 4 x 4 m room with a 2 x 2 m corner cut out (area 12), clockwise as seen from above.
            var room = new[]
            {
                new Vector2(0, 0), new Vector2(0, 4), new Vector2(2, 4), new Vector2(2, 2), new Vector2(4, 2), new Vector2(4, 0),
            };
            var triangles = new List<int>();

            Assert.That(PolygonTriangulator.Triangulate(room, triangles), Is.True);

            Assert.That(triangles, Has.Count.EqualTo((room.Length - 2) * 3));
            Assert.That(TriangulatedArea(room, triangles), Is.EqualTo(12f).Within(1e-4f));
        }

        [Test]
        public void Triangulate_DegenerateInput_ReturnsFalse()
        {
            var triangles = new List<int>();

            Assert.That(PolygonTriangulator.Triangulate(new[] { Vector2.zero, Vector2.one }, triangles), Is.False);
            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void Triangulate_WindsTrianglesUpwardForUnitysFrontFaces()
        {
            var counterClockwise = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var triangles = new List<int>();
            PolygonTriangulator.Triangulate(counterClockwise, triangles);

            // Plane space (x, y) maps to world (x, z); a clockwise triangle seen from +Y faces up in Unity.
            Vector3 P(int i) => new(counterClockwise[i].x, 0f, counterClockwise[i].y);
            var normal = Vector3.Cross(P(triangles[1]) - P(triangles[0]), P(triangles[2]) - P(triangles[0]));
            Assert.That(normal.y, Is.GreaterThan(0f));
        }
    }
}
