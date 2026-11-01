using System.Collections.Generic;
using Jolly.Core.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class FallbackFloorAreaTests
    {
        [Test]
        public void Outline_IsARegularPolygonOnTheCircle()
        {
            var area = new FallbackFloorArea(1.5f, 0.5f, 24, 0.5f, 4f);
            area.Place(Vector3.zero, Vector3.zero);

            Assert.That(area.Outline.Count, Is.EqualTo(24));
            foreach (var corner in area.Outline)
                Assert.That(corner.magnitude, Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(SignedArea(area.Outline), Is.EqualTo(0.5f * 24 * 1.5f * 1.5f * Mathf.Sin(2f * Mathf.PI / 24)).Within(1e-4f),
                "positive: counter-clockwise, and close to the circle's area");
        }

        [TestCase(0)]
        [TestCase(2)]
        public void Outline_HasAtLeastThreeCorners(int segments)
        {
            var outline = new List<Vector2>();

            FallbackFloorArea.BuildOutline(1f, segments, outline);

            Assert.That(outline.Count, Is.EqualTo(3));
        }

        [Test]
        public void Place_CentresOnTheUsersFloorPosition()
        {
            var area = Area();

            Assert.That(area.NeedsPlacement(new Vector3(1f, 1.6f, 2f), new Vector3(1f, 0f, 3f)), Is.True, "not placed yet");
            Assert.That(area.Place(new Vector3(1f, 1.6f, 2f), new Vector3(1f, 0f, 3f)), Is.True);

            Assert.That(area.IsPlaced, Is.True);
            Assert.That(area.Center, Is.EqualTo(new Vector3(1f, 0f, 2f)));
        }

        [Test]
        public void NeedsPlacement_OnlyOnceTheUserLeavesTheRecentreRadius()
        {
            var area = Area();
            area.Place(Vector3.zero, Nubi);

            Assert.That(area.NeedsPlacement(new Vector3(0.3f, 1.7f, 0.39f), Nubi), Is.False, "0.49 m away; height is ignored");
            Assert.That(area.NeedsPlacement(new Vector3(0.3f, 1.7f, 0.41f), Nubi), Is.True, "0.51 m away");
        }

        [Test]
        public void Clear_NeedsPlacementAgain()
        {
            var area = Area();
            area.Place(Vector3.zero, Nubi);

            area.Clear();

            Assert.That(area.IsPlaced, Is.False);
            Assert.That(area.NeedsPlacement(Vector3.zero, Nubi), Is.True);
            Assert.That(area.Contains(Vector3.zero, 0f), Is.False);
        }

        [Test]
        public void Place_NubiOutsideTheUsersDisc_StretchesAConvexHullOverBoth()
        {
            var area = Area();
            var nubi = new Vector3(3f, 0f, 0f);

            Assert.That(area.Place(Vector3.zero, nubi), Is.True);

            Assert.That(area.Contains(nubi, FallbackFloorArea.CharacterMargin), Is.True);
            Assert.That(area.Contains(Vector3.zero, 1f), Is.True, "the user keeps their disc");
            Assert.That(area.Contains(new Vector3(1.5f, 0f, 0f), 0.4f), Is.True, "the strip between them");
            Assert.That(area.Contains(new Vector3(3.6f, 0f, 0f), 0f), Is.False, "nothing past Nubi's 0.5 m disc");
            Assert.That(area.Contains(new Vector3(3f, 0f, 1f), 0f), Is.False, "the strip narrows towards Nubi");
            AssertConvexCounterClockwise(area.Outline);
        }

        [Test]
        public void Place_NubiInsideTheUsersDisc_IsJustTheDisc()
        {
            var area = Area();

            area.Place(Vector3.zero, new Vector3(0.5f, 0f, 0f));

            Assert.That(area.Outline.Count, Is.EqualTo(16));
            foreach (var corner in area.Outline)
                Assert.That(corner.magnitude, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void Place_NubiPastTheMaximumSpan_IsTheUsersDiscAndAsksForAReturn()
        {
            var area = Area();
            var nubi = new Vector3(0f, 0.02f, 5f);

            Assert.That(area.Place(new Vector3(1f, 1.6f, 0f), nubi), Is.False);

            Assert.That(area.Outline.Count, Is.EqualTo(16));
            Assert.That(area.Contains(nubi, 0f), Is.False);
            var back = area.ReturnPoint(nubi);
            Assert.That(back.y, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(back, new Vector3(1f, 0f, 0f)), Is.EqualTo(1f).Within(1e-4f), "1 m from the user");
            Assert.That(Vector3.Dot((back - new Vector3(1f, 0f, 0f)).normalized, new Vector3(-1f, 0f, 5f).normalized),
                Is.GreaterThan(0.999f), "on Nubi's side");
            Assert.That(area.Contains(back, FallbackFloorArea.CharacterMargin), Is.True);
        }

        [Test]
        public void ReturnPoint_NubiOnTheUser_StillGivesAPointInside()
        {
            var area = Area();
            area.Place(Vector3.zero, Vector3.zero);

            Assert.That(area.Contains(area.ReturnPoint(Vector3.zero), FallbackFloorArea.CharacterMargin), Is.True);
        }

        [Test]
        public void NeedsPlacement_WhenNubiLeavesTheArea_EvenIfTheUserStays()
        {
            var area = Area();
            area.Place(Vector3.zero, Nubi);

            Assert.That(area.NeedsPlacement(Vector3.zero, new Vector3(1.1f, 0f, 0f)), Is.False, "inside, past the margin");
            Assert.That(area.NeedsPlacement(Vector3.zero, new Vector3(1.4f, 0f, 0f)), Is.True, "too close to the edge");
            Assert.That(area.NeedsPlacement(Vector3.zero, new Vector3(2.5f, 0f, 0f)), Is.True, "outside");
        }

        static readonly Vector3 Nubi = new(0f, 0f, 0.8f);

        static FallbackFloorArea Area() => new(1.5f, 0.5f, 16, 0.5f, 4f);

        static void AssertConvexCounterClockwise(IReadOnlyList<Vector2> polygon)
        {
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var c = polygon[(i + 2) % polygon.Count];
                var turn = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                Assert.That(turn, Is.GreaterThan(0f), "corner " + i);
            }
        }

        static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            var sum = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                sum += a.x * b.y - b.x * a.y;
            }
            return sum * 0.5f;
        }
    }
}
