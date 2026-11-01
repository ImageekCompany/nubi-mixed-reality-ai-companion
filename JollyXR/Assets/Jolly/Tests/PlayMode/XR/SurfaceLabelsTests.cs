using Jolly.XR.World;
using NUnit.Framework;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class SurfaceLabelsTests
    {
        [TestCase(BoundingBoxClassifications.Couch, "couch")]
        [TestCase(BoundingBoxClassifications.Table, "table")]
        [TestCase(BoundingBoxClassifications.Bed, "bed")]
        [TestCase(BoundingBoxClassifications.Chair, "chair")]
        [TestCase(BoundingBoxClassifications.Storage, "storage")]
        [TestCase(BoundingBoxClassifications.Screen, "screen")]
        [TestCase(BoundingBoxClassifications.Table | BoundingBoxClassifications.Lamp, "table")]
        public void ForBox_NamesFurniture(BoundingBoxClassifications classifications, string label)
        {
            Assert.That(SurfaceLabels.ForBox(classifications), Is.EqualTo(label));
        }

        [TestCase(PlaneClassifications.Couch, "couch")]
        [TestCase(PlaneClassifications.Table, "table")]
        [TestCase(PlaneClassifications.Seat, "chair")]
        public void ForPlane_NamesFurniture(PlaneClassifications classifications, string label)
        {
            Assert.That(SurfaceLabels.ForPlane(classifications), Is.EqualTo(label));
        }

        [Test]
        public void NonFurniture_HasNoLabel()
        {
            Assert.That(SurfaceLabels.ForBox(BoundingBoxClassifications.None), Is.Null);
            Assert.That(SurfaceLabels.ForBox(BoundingBoxClassifications.Bathtub), Is.Null);
            Assert.That(SurfaceLabels.ForPlane(PlaneClassifications.Floor), Is.Null);
            Assert.That(SurfaceLabels.ForPlane(PlaneClassifications.WallFace), Is.Null);
        }
    }
}
