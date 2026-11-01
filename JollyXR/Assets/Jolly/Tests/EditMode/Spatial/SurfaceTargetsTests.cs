using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.Core.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class SurfaceTargetsTests
    {
        // A 2 x 0.8 x 1 m couch centred at (0, 0.4, 2), turned 90° so its long side runs along world z.
        static readonly SceneSurface TurnedCouch =
            new("couch", new Pose(new Vector3(0f, 0.4f, 2f), Quaternion.Euler(0f, 90f, 0f)), new Vector3(2f, 0.8f, 1f));

        static SceneSurface Box(string label, Vector3 center, Vector3 size) => new(label, new Pose(center, Quaternion.identity), size);

        [Test]
        public void TryNearest_PicksTheClosestCentreWithTheLabel_IgnoringCaseAndHeight()
        {
            var surfaces = new List<SceneSurface>
            {
                Box("table", new Vector3(3f, 0.4f, 0f), Vector3.one),
                Box("couch", new Vector3(0.5f, 0.4f, 0f), Vector3.one),
                Box("table", new Vector3(-1.5f, 5f, 0f), Vector3.one),
            };

            Assert.That(SurfaceTargets.TryNearest(surfaces, "TABLE", new Vector3(0f, 1.6f, 0f), out var nearest), Is.True);
            Assert.That(nearest.Pose.position.x, Is.EqualTo(-1.5f));
            Assert.That(SurfaceTargets.TryNearest(surfaces, "bed", Vector3.zero, out _), Is.False);
        }

        [Test]
        public void Approach_FromOutside_StopsClearanceBeyondTheNearestEdge()
        {
            var table = Box("table", new Vector3(0f, 0.375f, 2f), new Vector3(1.2f, 0.75f, 0.8f));

            var spot = SurfaceTargets.Approach(table, new Vector3(0.2f, 0f, 0f), 0.25f);

            Assert.That(Vector3.Distance(spot, new Vector3(0.2f, 0f, 1.35f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Approach_FromACorner_StepsOutDiagonally()
        {
            var table = Box("table", Vector3.zero, new Vector3(1f, 0.75f, 1f));

            var spot = SurfaceTargets.Approach(table, new Vector3(2f, 0f, 2f), 0.25f);

            var expected = new Vector3(0.5f, 0f, 0.5f) + new Vector3(1f, 0f, 1f).normalized * 0.25f;
            Assert.That(Vector3.Distance(spot, expected), Is.LessThan(1e-4f));
        }

        [Test]
        public void Approach_FollowsTheSurfaceYaw()
        {
            var spot = SurfaceTargets.Approach(TurnedCouch, new Vector3(-3f, 0f, 2f), 0.25f);

            Assert.That(Vector3.Distance(spot, new Vector3(-0.75f, 0f, 2f)), Is.LessThan(1e-4f),
                "turned 90°, the 1 m depth now spans x: half 0.5 m plus clearance");
        }

        [Test]
        public void Approach_FromInsideTheFootprint_LeavesByTheNearestEdge()
        {
            var bed = Box("bed", Vector3.zero, new Vector3(2f, 0.5f, 1.6f));

            var spot = SurfaceTargets.Approach(bed, new Vector3(0.9f, 0f, 0.1f), 0.25f);

            Assert.That(Vector3.Distance(spot, new Vector3(1.25f, 0f, 0.1f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Approach_KeepsTheCallersHeight()
        {
            var table = Box("table", new Vector3(0f, 0.375f, 2f), new Vector3(1f, 0.75f, 1f));

            Assert.That(SurfaceTargets.Approach(table, new Vector3(0f, 0.02f, 0f), 0.25f).y, Is.EqualTo(0.02f));
        }

        [Test]
        public void LookPoint_IsTheCentreOfTheTop_ForBoxesAndPlanes()
        {
            var plane = new SceneSurface("table", new Pose(new Vector3(1f, 0.72f, 1f), Quaternion.identity), new Vector3(1f, 0f, 1f));

            Assert.That(SurfaceTargets.LookPoint(TurnedCouch), Is.EqualTo(new Vector3(0f, 0.8f, 2f)));
            Assert.That(SurfaceTargets.LookPoint(plane), Is.EqualTo(new Vector3(1f, 0.72f, 1f)));
        }
    }
}
