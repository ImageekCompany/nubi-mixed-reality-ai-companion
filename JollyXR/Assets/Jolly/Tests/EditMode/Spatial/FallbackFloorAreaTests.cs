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
            var area = new FallbackFloorArea(1.5f, 24, 0.5f);

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
            var area = new FallbackFloorArea(1.5f, 16, 0.5f);

            Assert.That(area.NeedsPlacement(new Vector3(1f, 1.6f, 2f)), Is.True, "not placed yet");
            area.Place(new Vector3(1f, 1.6f, 2f));

            Assert.That(area.IsPlaced, Is.True);
            Assert.That(area.Center, Is.EqualTo(new Vector3(1f, 0f, 2f)));
        }

        [Test]
        public void NeedsPlacement_OnlyOnceTheUserLeavesTheRecentreRadius()
        {
            var area = new FallbackFloorArea(1.5f, 16, 0.5f);
            area.Place(Vector3.zero);

            Assert.That(area.NeedsPlacement(new Vector3(0.3f, 1.7f, 0.39f)), Is.False, "0.49 m away; height is ignored");
            Assert.That(area.NeedsPlacement(new Vector3(0.3f, 1.7f, 0.41f)), Is.True, "0.51 m away");
        }

        [Test]
        public void Clear_NeedsPlacementAgain()
        {
            var area = new FallbackFloorArea(1.5f, 16, 0.5f);
            area.Place(Vector3.zero);

            area.Clear();

            Assert.That(area.IsPlaced, Is.False);
            Assert.That(area.NeedsPlacement(Vector3.zero), Is.True);
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
