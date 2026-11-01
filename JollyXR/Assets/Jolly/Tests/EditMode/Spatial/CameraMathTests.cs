using Jolly.Contracts;
using Jolly.Core.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Jolly.Tests.EditMode.Spatial
{
    public sealed class CameraMathTests
    {
        // Quest 3 left passthrough camera at 1728² (device-spike.md §1), scaled to the 640 px upload size.
        static readonly CameraIntrinsics Quest640 =
            new CameraIntrinsics(new Vector2(1172.536f, 1172.536f), new Vector2(869.562f, 861.484f), new Vector2Int(1728, 1728))
                .Scaled(640f / 1728f);

        static readonly Pose Tilted = new(new Vector3(0.3f, 1.6f, -0.2f), Quaternion.Euler(12f, -35f, 4f));

        [Test]
        public void PixelToWorldRay_AtPrincipalPoint_PointsAlongCameraForward()
        {
            var ray = CameraMath.PixelToWorldRay(Quest640.PrincipalPoint, Quest640, Tilted);

            Assert.That(Vector3.Distance(ray.origin, Tilted.position), Is.LessThan(1e-5f));
            Assert.That(Vector3.Angle(ray.direction, Tilted.rotation * Vector3.forward), Is.LessThan(0.01f));
        }

        [Test]
        public void PixelToWorldRay_TopLeftPixel_PointsUpAndLeft()
        {
            var ray = CameraMath.PixelToWorldRay(Vector2.zero, Quest640, Pose.identity);

            Assert.That(ray.direction.x, Is.LessThan(0f), "pixel x grows to the right");
            Assert.That(ray.direction.y, Is.GreaterThan(0f), "pixel y grows downwards, the camera's +Y is image up");
            Assert.That(ray.direction.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(0f, 0f)]
        [TestCase(320f, 320f)]
        [TestCase(12.5f, 600.25f)]
        [TestCase(639f, 1f)]
        public void PixelToRayToPixel_RoundTrips(float x, float y)
        {
            var px = new Vector2(x, y);
            var ray = CameraMath.PixelToWorldRay(px, Quest640, Tilted);

            var visible = CameraMath.WorldToPixel(ray.GetPoint(1.37f), Quest640, Tilted, out var back);

            Assert.That(visible, Is.True);
            Assert.That(Vector2.Distance(back, px), Is.LessThan(1e-3f));
        }

        [Test]
        public void WorldToPixelToRay_PassesThroughThePoint()
        {
            var world = Tilted.position + Tilted.rotation * new Vector3(0.12f, -0.08f, 0.9f);

            Assert.That(CameraMath.WorldToPixel(world, Quest640, Tilted, out var px), Is.True);
            var ray = CameraMath.PixelToWorldRay(px, Quest640, Tilted);

            var closest = ray.GetPoint(Vector3.Dot(world - ray.origin, ray.direction));
            Assert.That(Vector3.Distance(closest, world), Is.LessThan(1e-4f));
        }

        [Test]
        public void WorldToPixel_BehindTheCamera_ReturnsFalse()
        {
            var behind = Tilted.position - Tilted.rotation * Vector3.forward;

            Assert.That(CameraMath.WorldToPixel(behind, Quest640, Tilted, out _), Is.False);
        }

        [Test]
        public void WorldToPixel_OutsideTheImage_ReturnsFalseButStillProjects()
        {
            var farRight = new Vector3(5f, 0f, 1f);

            Assert.That(CameraMath.WorldToPixel(farRight, Quest640, Pose.identity, out var px), Is.False);
            Assert.That(px.x, Is.GreaterThan(Quest640.Resolution.x));
        }
    }
}
