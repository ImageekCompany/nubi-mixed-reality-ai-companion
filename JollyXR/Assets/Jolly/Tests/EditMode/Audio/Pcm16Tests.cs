using Jolly.Audio.Resampling;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Audio
{
    public sealed class Pcm16Tests
    {
        [Test]
        public void FromFloat_ScalesRoundsAndClamps()
        {
            var input = new[] { 0f, 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f };
            var output = new short[input.Length];

            Pcm16.FromFloat(input, output);

            Assert.That(output, Is.EqualTo(new short[] { 0, 16384, -16384, 32767, -32767, 32767, -32767 }));
        }

        [Test]
        public void Rms_OfAFullScaleSquareWave_IsOne()
        {
            var samples = new short[] { 32767, -32767, 32767, -32767 };

            Assert.That(Pcm16.Rms(samples), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Rms_OfNothing_IsZero()
        {
            Assert.That(Pcm16.Rms(new short[0]), Is.Zero);
        }
    }
}
