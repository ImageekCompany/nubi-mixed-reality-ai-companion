using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Testing;
using Jolly.XR.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.PlayMode.XR
{
    public sealed class EditorStillFrameSourceTests
    {
        static readonly Color32 TopLeft = new(255, 0, 0, 255);
        static readonly Color32 BottomRight = new(0, 0, 255, 255);

        // 8x4 PNG: red top-left quadrant, blue bottom-right quadrant, grey elsewhere.
        static byte[] QuadrantPng()
        {
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[32];
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 8; x++)
            {
                var top = y >= 2; // Texture2D row 0 is the bottom
                pixels[y * 8 + x] = top && x < 4 ? TopLeft : !top && x >= 4 ? BottomRight : new Color32(128, 128, 128, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        static Color32 PixelAt(CameraFrame frame, int x, int y)
        {
            var i = (y * frame.Width + x) * 4;
            return new Color32(frame.Rgba32[i], frame.Rgba32[i + 1], frame.Rgba32[i + 2], frame.Rgba32[i + 3]);
        }

        [UnityTest]
        public IEnumerator Capture_DeliversTopRowFirstAtTheRequestedSize() => UniTask.ToCoroutine(async () =>
        {
            var pose = new FakeUserPoseProvider { Head = new Pose(Vector3.up, Quaternion.Euler(0f, 30f, 0f)) };
            var clock = new FakeClock();
            clock.Advance(System.TimeSpan.FromSeconds(3));
            using var source = new EditorStillFrameSource(QuadrantPng(), pose, clock);

            using var frame = await source.Capture(4, CancellationToken.None);

            Assert.That(source.Availability, Is.EqualTo(CameraAvailability.Ready));
            Assert.That(frame.Width, Is.EqualTo(4));
            Assert.That(frame.Height, Is.EqualTo(2));
            Assert.That(PixelAt(frame, 0, 0), Is.EqualTo(TopLeft));
            Assert.That(PixelAt(frame, 3, 1), Is.EqualTo(BottomRight));
            Assert.That(frame.CameraPoseWorld, Is.EqualTo(pose.Head));
            Assert.That(frame.Timestamp, Is.EqualTo(3.0));
        });

        [UnityTest]
        public IEnumerator Capture_ScalesIntrinsicsToTheDeliveredImage() => UniTask.ToCoroutine(async () =>
        {
            using var source = new EditorStillFrameSource(QuadrantPng(), new FakeUserPoseProvider(), new FakeClock());

            using var full = await source.Capture(8, CancellationToken.None);
            var fullK = full.Intrinsics;
            full.Dispose();
            using var half = await source.Capture(4, CancellationToken.None);

            Assert.That(fullK.PrincipalPoint, Is.EqualTo(new Vector2(4f, 2f)));
            Assert.That(half.Intrinsics.FocalLength.x, Is.EqualTo(fullK.FocalLength.x / 2f).Within(1e-4f));
            Assert.That(half.Intrinsics.Resolution, Is.EqualTo(new Vector2Int(4, 2)));
        });

        [Test]
        public void Construct_WithUndecodableBytes_IsNotSupported()
        {
            LogAssert.ignoreFailingMessages = true;
            using var source = new EditorStillFrameSource(new byte[] { 1, 2, 3 }, new FakeUserPoseProvider(), new FakeClock());

            Assert.That(source.Availability, Is.EqualTo(CameraAvailability.NotSupported));
        }
    }
}
