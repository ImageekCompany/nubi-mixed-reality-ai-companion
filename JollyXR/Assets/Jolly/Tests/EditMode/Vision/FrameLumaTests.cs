using System;
using Jolly.Core.Imaging;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class FrameLumaTests
    {
        const string BundledStill = "Assets/Jolly/Content/SelfTest/still_mug.bytes";

        [Test]
        public void Black_IsZero()
        {
            Assert.That(FrameLuma.Mean(Solid(64, 0, 0, 0)), Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void MidGrey_IsItsValue()
        {
            Assert.That(FrameLuma.Mean(Solid(64, 128, 128, 128)), Is.EqualTo(128).Within(1e-6));
        }

        [Test]
        public void White_IsFullScale()
        {
            Assert.That(FrameLuma.Mean(Solid(64, 255, 255, 255)), Is.EqualTo(255).Within(1e-6));
        }

        [Test]
        public void Channels_AreWeightedRec601AndAlphaIsIgnored()
        {
            var red = Solid(4, 255, 0, 0, alpha: 0);

            Assert.That(FrameLuma.Mean(red), Is.EqualTo(0.299 * 255).Within(1e-6));
        }

        [Test]
        public void EmptyFrame_IsNaN()
        {
            Assert.That(FrameLuma.Mean(ReadOnlySpan<byte>.Empty), Is.NaN);
        }

        [Test]
        public void LargeFrame_IsSampledButKeepsTheMean()
        {
            // 640×480 is past the sample cap: half black rows, half white rows must still average to half scale.
            const int width = 640, height = 480;
            var pixels = new byte[width * height * 4];
            for (var y = height / 2; y < height; y++)
                pixels.AsSpan(y * width * 4, width * 4).Fill(255);

            Assert.That(FrameLuma.Mean(pixels), Is.EqualTo(127.5).Within(2.0));
        }

        [Test]
        public void BundledSelfTestStill_IsBrighterThanTheDarkRoomThreshold()
        {
            var still = AssetDatabase.LoadAssetAtPath<TextAsset>(BundledStill);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(still.bytes), Is.True);
                var colors = texture.GetPixels32();
                var rgba = new byte[colors.Length * 4];
                for (var i = 0; i < colors.Length; i++)
                {
                    rgba[i * 4] = colors[i].r;
                    rgba[i * 4 + 1] = colors[i].g;
                    rgba[i * 4 + 2] = colors[i].b;
                }

                Assert.That(FrameLuma.Mean(rgba), Is.GreaterThan(new Jolly.Core.Config.VisionSettings().MinMeanLuma));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static byte[] Solid(int side, byte r, byte g, byte b, byte alpha = 255)
        {
            var pixels = new byte[side * side * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = r;
                pixels[i + 1] = g;
                pixels[i + 2] = b;
                pixels[i + 3] = alpha;
            }
            return pixels;
        }
    }
}
