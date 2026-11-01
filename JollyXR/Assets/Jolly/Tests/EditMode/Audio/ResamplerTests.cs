using System;
using Jolly.Audio.Resampling;
using NUnit.Framework;

namespace Jolly.Tests.EditMode.Audio
{
    public sealed class ResamplerTests
    {
        [Test]
        public void SameRate_PassesSamplesThroughUnchanged()
        {
            var resampler = new Resampler(24000, 24000);
            var input = new[] { 0.1f, -0.2f, 0.3f, -0.4f };
            var output = new float[resampler.MaxOutputLength(input.Length)];

            var count = resampler.Process(input, output);

            Assert.That(count, Is.EqualTo(4));
            Assert.That(new ArraySegment<float>(output, 0, count), Is.EqualTo(input));
        }

        [Test]
        public void Downsample48To24_HalvesTheSampleCount()
        {
            var output = Resample(48000, 24000, Sine(1000, 48000, 4800));

            Assert.That(output.Length, Is.InRange(2399, 2401));
        }

        [Test]
        public void Downsample48To24_KeepsA1KHzToneWithin1Percent()
        {
            var output = Resample(48000, 24000, Sine(1000, 48000, 9600));

            var amplitude = ToneAmplitude(output, 1000, 24000, skip: 100, length: 2400);

            Assert.That(amplitude, Is.EqualTo(0.5).Within(0.005));
        }

        [Test]
        public void Downsample48To24_RejectsToneAboveTheOutputNyquistBy40Db()
        {
            var output = Resample(48000, 24000, Sine(18000, 48000, 9600));

            var alias = ToneAmplitude(output, 6000, 24000, skip: 100, length: 2400);

            Assert.That(20 * Math.Log10(alias / 0.5), Is.LessThan(-40));
        }

        [Test]
        public void Upsample24To48_KeepsA1KHzToneWithin1Percent()
        {
            var output = Resample(24000, 48000, Sine(1000, 24000, 4800));

            var amplitude = ToneAmplitude(output, 1000, 48000, skip: 96, length: 4800);

            Assert.That(output.Length, Is.InRange(9597, 9600));
            Assert.That(amplitude, Is.EqualTo(0.5).Within(0.005));
        }

        [Test]
        public void NonIntegerRatio_ProducesTheProportionalSampleCount()
        {
            var output = Resample(24000, 44100, Sine(440, 24000, 24000));

            Assert.That(output.Length, Is.InRange(44097, 44100));
        }

        [Test]
        public void Output_DoesNotDependOnHowTheInputIsSplit()
        {
            var input = Sine(1000, 48000, 5000);
            var whole = Resample(48000, 24000, input);
            var resampler = new Resampler(48000, 24000);
            var pieces = new float[whole.Length + 16];
            var written = 0;
            var offset = 0;
            var sizes = new[] { 1, 7, 480, 3, 960, 1 };

            for (var i = 0; offset < input.Length; i++)
            {
                var size = Math.Min(sizes[i % sizes.Length], input.Length - offset);
                written += resampler.Process(new ReadOnlySpan<float>(input, offset, size), pieces.AsSpan(written));
                offset += size;
            }

            Assert.That(written, Is.EqualTo(whole.Length));
            for (var i = 0; i < written; i++)
                Assert.That(pieces[i], Is.EqualTo(whole[i]).Within(1e-6f), $"sample {i}");
        }

        [Test]
        public void Reset_ForgetsPreviousInput()
        {
            var input = Sine(1000, 48000, 960);
            var resampler = new Resampler(48000, 24000);
            var first = new float[resampler.MaxOutputLength(input.Length)];
            var second = new float[first.Length];
            var firstCount = resampler.Process(input, first);

            resampler.Reset();
            var secondCount = resampler.Process(input, second);

            Assert.That(secondCount, Is.EqualTo(firstCount));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Process_RejectsAnOutputThatMayBeTooSmall()
        {
            var resampler = new Resampler(24000, 48000);

            Assert.That(() => resampler.Process(new float[100], new float[100]), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_RejectsNonPositiveRates()
        {
            Assert.That(() => new Resampler(0, 24000), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Resampler(48000, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        static float[] Resample(int from, int to, float[] input)
        {
            var resampler = new Resampler(from, to);
            var output = new float[resampler.MaxOutputLength(input.Length)];
            var count = resampler.Process(input, output);
            Array.Resize(ref output, count);
            return output;
        }

        static float[] Sine(double frequency, int sampleRate, int length)
        {
            var samples = new float[length];
            for (var i = 0; i < length; i++)
                samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
            return samples;
        }

        /// <summary>Single-bin DFT amplitude; length must hold a whole number of periods so the bin does not leak.</summary>
        static double ToneAmplitude(float[] samples, double frequency, int sampleRate, int skip, int length)
        {
            double re = 0, im = 0;
            for (var i = 0; i < length; i++)
            {
                var phase = 2 * Math.PI * frequency * i / sampleRate;
                re += samples[skip + i] * Math.Cos(phase);
                im += samples[skip + i] * Math.Sin(phase);
            }
            return 2 * Math.Sqrt(re * re + im * im) / length;
        }
    }
}
