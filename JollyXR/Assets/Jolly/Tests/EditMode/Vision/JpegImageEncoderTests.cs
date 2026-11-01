using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Imaging;
using Jolly.Tests.EditMode.Vision.Doubles;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class JpegImageEncoderTests
    {
        static readonly Color32 Red = new(230, 20, 20, 255);
        static readonly Color32 Blue = new(20, 20, 230, 255);
        static readonly Pose CameraPose = new(new Vector3(1, 2, 3), Quaternion.Euler(0, 90, 0));

        NativeArray<byte> _pixels;
        int _released;

        [SetUp]
        public void SetUp() => _released = 0;

        [TearDown]
        public void TearDown()
        {
            if (_pixels.IsCreated)
                _pixels.Dispose();
        }

        [UnityTest]
        public IEnumerator EncodeJpeg_CarriesSizeIntrinsicsAndPoseAndLeavesTheFrameOpen() => UniTask.ToCoroutine(async () =>
        {
            using var frame = Frame(64, 48, (_, _) => Red);

            var image = await new JpegImageEncoder().EncodeJpeg(frame, 80, CancellationToken.None);

            Assert.That(image.MimeType, Is.EqualTo("image/jpeg"));
            Assert.That((image.Width, image.Height), Is.EqualTo((64, 48)));
            Assert.That(image.Bytes[0], Is.EqualTo(0xFF));
            Assert.That(image.Bytes[1], Is.EqualTo(0xD8));
            Assert.That(image.Intrinsics.FocalLength, Is.EqualTo(frame.Intrinsics.FocalLength));
            Assert.That(image.CameraPoseWorld, Is.EqualTo(CameraPose));
            Assert.That(_released, Is.Zero);
        });

        [UnityTest]
        public IEnumerator EncodeJpeg_KeepsTheTopRowAtTheTop() => UniTask.ToCoroutine(async () =>
        {
            using var frame = Frame(32, 32, (_, row) => row < 16 ? Red : Blue);

            var image = await new JpegImageEncoder().EncodeJpeg(frame, 90, CancellationToken.None);

            var decoded = Decode(image.Bytes);
            try
            {
                Assert.That(IsReddish(decoded.GetPixel(16, 31)), Is.True, "texture y=31 is the image's top row");
                Assert.That(IsReddish(decoded.GetPixel(16, 0)), Is.False, "texture y=0 is the image's bottom row");
            }
            finally
            {
                Object.DestroyImmediate(decoded);
            }
        });

        [UnityTest]
        public IEnumerator EncodeJpeg_RealPhoto_RoundTripsAtTheSameSizeAndResumesOnTheMainThread() => UniTask.ToCoroutine(async () =>
        {
            var source = Decode(VisionFixtures.Image("pen_640.jpg"));
            var pixels = source.GetPixels32();
            var (width, height) = (source.width, source.height);
            Object.DestroyImmediate(source);
            using var frame = Frame(width, height, (x, row) => pixels[(height - 1 - row) * width + x]);

            var image = await new JpegImageEncoder().EncodeJpeg(frame, 80, CancellationToken.None);

            Assert.That(PlayerLoopHelper.IsMainThread, Is.True);
            var decoded = Decode(image.Bytes);
            Assert.That((decoded.width, decoded.height), Is.EqualTo((640, 428)));
            Object.DestroyImmediate(decoded);
        });

        [UnityTest]
        public IEnumerator EncodeJpeg_AlreadyCancelled_ThrowsWithoutTouchingTheFrame() => UniTask.ToCoroutine(async () =>
        {
            using var frame = Frame(8, 8, (_, _) => Red);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await AsyncAssert.Throws<OperationCanceledException>(() => new JpegImageEncoder().EncodeJpeg(frame, 80, cts.Token));
        });

        [TestCase(0)]
        [TestCase(101)]
        public void EncodeJpeg_QualityOutOfRange_Throws(int quality)
        {
            using var frame = Frame(8, 8, (_, _) => Red);

            Assert.Throws<ArgumentOutOfRangeException>(() => new JpegImageEncoder().EncodeJpeg(frame, quality, CancellationToken.None));
        }

        /// <summary>Builds a frame the way the camera source does: RGBA32, row-major, top row first.</summary>
        CameraFrame Frame(int width, int height, Func<int, int, Color32> colorAt)
        {
            _pixels = new NativeArray<byte>(width * height * 4, Allocator.Persistent);
            for (var row = 0; row < height; row++)
            {
                for (var x = 0; x < width; x++)
                {
                    var c = colorAt(x, row);
                    var i = (row * width + x) * 4;
                    _pixels[i] = c.r; _pixels[i + 1] = c.g; _pixels[i + 2] = c.b; _pixels[i + 3] = c.a;
                }
            }
            var intrinsics = new CameraIntrinsics(new Vector2(400, 400), new Vector2(width / 2f, height / 2f), new Vector2Int(width, height));
            return new CameraFrame(_pixels, width, height, intrinsics, CameraPose, 12.5, _ => _released++);
        }

        static Texture2D Decode(byte[] jpeg)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(jpeg), Is.True);
            return texture;
        }

        static bool IsReddish(Color c) => c.r > 0.6f && c.b < 0.4f;
    }
}
