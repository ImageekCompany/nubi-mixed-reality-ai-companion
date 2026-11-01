using Jolly.XR.World;
using NUnit.Framework;
using UnityEngine.XR.ARSubsystems;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class SurfaceLabelsTests
    {
        [TestCase("couch", PlaneClassifications.Couch, BoundingBoxClassifications.Couch)]
        [TestCase(" Sofa ", PlaneClassifications.Couch, BoundingBoxClassifications.Couch)]
        [TestCase("TABLE", PlaneClassifications.Table, BoundingBoxClassifications.Table)]
        [TestCase("desk", PlaneClassifications.Table, BoundingBoxClassifications.Table)]
        [TestCase("bed", PlaneClassifications.None, BoundingBoxClassifications.Bed)]
        [TestCase("tv", PlaneClassifications.None, BoundingBoxClassifications.Screen)]
        [TestCase("storage", PlaneClassifications.None, BoundingBoxClassifications.Storage)]
        [TestCase("chair", PlaneClassifications.Seat, BoundingBoxClassifications.Chair)]
        public void TryParse_KnownLabelsAndAliases(string label, PlaneClassifications plane, BoundingBoxClassifications box)
        {
            Assert.That(SurfaceLabels.TryParse(label, out var p, out var b), Is.True);
            Assert.That(p, Is.EqualTo(plane));
            Assert.That(b, Is.EqualTo(box));
        }

        [TestCase("kitchen")]
        [TestCase("")]
        [TestCase(null)]
        public void TryParse_UnknownLabel_False(string label)
        {
            Assert.That(SurfaceLabels.TryParse(label, out _, out _), Is.False);
        }
    }
}
