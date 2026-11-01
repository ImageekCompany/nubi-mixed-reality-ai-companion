using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Jolly.Contracts;
using Jolly.Providers.MetaApi.Vision;
using Jolly.Tests.EditMode.Vision.Doubles;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Vision
{
    public sealed class OneBitMaskDecoderTests
    {
        static readonly Regex MaskData = new(@"data=(\d+),(\d+),!([^|]*)\|>");

        // Expected values come from the independent research decoder (meta-model-api.md §4.3), whose output PNGs
        // were checked by eye; raster SHA-256 is over one byte (0/1) per pixel, row-major.
        [TestCase(VisionFixtures.PenConcept, 323, 372, 13083, 0.109, "b09cfa05bb8232d4")]
        [TestCase(VisionFixtures.MugConcept, 189, 131, 17933, 0.724, "debbb6ffc303ae49")]
        public void Decode_LiveFixture_MatchesReferenceRaster(string fixture, int width, int height, int ones,
                                                              double coverage, string sha256Prefix)
        {
            var (w, h, payload) = Payload(fixture);

            var mask = OneBitMaskDecoder.Decode(payload.AsSpan(), w, h);

            Assert.That((mask.Width, mask.Height), Is.EqualTo((width, height)));
            Assert.That(CountOnes(mask), Is.EqualTo(ones));
            Assert.That((double)ones / (width * height), Is.EqualTo(coverage).Within(0.0005));
            Assert.That(Sha256Prefix(mask), Is.EqualTo(sha256Prefix));
        }

        [Test]
        public void Decode_PenFixture_HasClearCornerAndFilledCentre()
        {
            var (w, h, payload) = Payload(VisionFixtures.PenConcept);

            var mask = OneBitMaskDecoder.Decode(payload.AsSpan(), w, h);

            Assert.That(mask[0, 0], Is.False);
            Assert.That(mask[w / 2, h / 2], Is.True);
        }

        [TestCase(VisionFixtures.PenConcept)]
        [TestCase(VisionFixtures.MugConcept)]
        public void Decode_ThenReferenceEncode_ReproducesThePayload(string fixture)
        {
            var (w, h, payload) = Payload(fixture);

            var mask = OneBitMaskDecoder.Decode(payload.AsSpan(), w, h);

            Assert.That(OneBitMaskEncoder.Encode(ToRaster(mask), w, h), Is.EqualTo(payload));
        }

        [TestCase(1, 1, 0.0)]
        [TestCase(17, 9, 0.5)]
        [TestCase(64, 3, 1.0)]
        [TestCase(130, 97, 0.3)]
        public void Decode_OfEncodedSyntheticRaster_RoundTrips(int width, int height, double density)
        {
            var random = new Random(width * 7919 + height);
            var raster = new byte[width * height];
            for (var i = 0; i < raster.Length; i++)
                raster[i] = (byte)(random.NextDouble() < density ? 1 : 0);

            var mask = OneBitMaskDecoder.Decode(OneBitMaskEncoder.Encode(raster, width, height).AsSpan(), width, height);

            Assert.That(ToRaster(mask), Is.EqualTo(raster));
        }

        [Test]
        public void Decode_PayloadLongerThanItsPrefix_Throws()
        {
            var (w, h, payload) = Payload(VisionFixtures.MugConcept);

            Assert.Throws<FormatException>(() => OneBitMaskDecoder.Decode((payload + "!").AsSpan(), w, h));
        }

        [Test]
        public void Decode_PayloadWithExcludedCharacter_Throws()
        {
            var (w, h, payload) = Payload(VisionFixtures.MugConcept);
            var corrupted = payload.Substring(0, 10) + "|" + payload.Substring(11);

            Assert.Throws<FormatException>(() => OneBitMaskDecoder.Decode(corrupted.AsSpan(), w, h));
        }

        [Test]
        public void Decode_MissingLengthPrefix_Throws()
        {
            Assert.Throws<FormatException>(() => OneBitMaskDecoder.Decode("!!!".AsSpan(), 4, 4));
        }

        [Test]
        public void Decode_RasterLargerThanThePayloadEncodes_ThrowsInsteadOfReadingPastTheEnd()
        {
            var payload = OneBitMaskEncoder.Encode(new byte[4], 2, 2);

            Assert.Throws<FormatException>(() => OneBitMaskDecoder.Decode(payload.AsSpan(), 1024, 1024));
        }

        [Test]
        public void Decode_NonPositiveSize_Throws()
        {
            Assert.Throws<FormatException>(() => OneBitMaskDecoder.Decode("!!!!#".AsSpan(), 0, 3));
        }

        [TestCase(4097, 1)]
        [TestCase(1, 4097)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void Decode_SizeBeyondAnyCameraFrame_ThrowsBeforeAllocating(int width, int height)
        {
            Assert.Throws<FormatException>(() => OneBitMaskDecoder.Decode("!!!!#".AsSpan(), width, height));
        }

        static (int width, int height, string payload) Payload(string fixture)
        {
            var match = MaskData.Match(VisionFixtures.SamOutputText(fixture));
            Assert.That(match.Success, Is.True, fixture);
            return (int.Parse(match.Groups[2].Value), int.Parse(match.Groups[1].Value), match.Groups[3].Value);
        }

        static byte[] ToRaster(BitMask mask)
        {
            var raster = new byte[mask.Width * mask.Height];
            for (var y = 0; y < mask.Height; y++)
            {
                for (var x = 0; x < mask.Width; x++)
                    raster[y * mask.Width + x] = (byte)(mask[x, y] ? 1 : 0);
            }
            return raster;
        }

        static int CountOnes(BitMask mask)
        {
            var count = 0;
            foreach (var b in ToRaster(mask))
                count += b;
            return count;
        }

        static string Sha256Prefix(BitMask mask)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(ToRaster(mask));
            return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToLowerInvariant();
        }
    }
}
