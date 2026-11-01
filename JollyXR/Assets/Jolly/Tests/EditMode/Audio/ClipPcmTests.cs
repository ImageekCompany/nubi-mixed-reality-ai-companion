using System;
using Jolly.Audio.Output;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Tests.EditMode.Audio
{
    public sealed class ClipPcmTests
    {
        [Test]
        public void ToChunk_SameRateMono_KeepsEverySample()
        {
            var clip = Clip(1, 24000, new[] { 0f, 0.5f, -0.5f, 1f });
            try
            {
                using var chunk = ClipPcm.ToChunk(clip, 24000);

                Assert.That(chunk.SampleRate, Is.EqualTo(24000));
                Assert.That(chunk.Span.ToArray(), Is.EqualTo(new short[] { 0, 16384, -16384, 32767 }));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ToChunk_Stereo48k_DownMixesAndHalvesTheRate()
        {
            const int frames = 4800;
            var interleaved = new float[frames * 2];
            for (var i = 0; i < frames; i++)
            {
                interleaved[2 * i] = 0.6f;
                interleaved[2 * i + 1] = 0.2f;
            }
            var clip = Clip(2, 48000, interleaved);
            try
            {
                using var chunk = ClipPcm.ToChunk(clip, 24000);

                Assert.That(chunk.SampleRate, Is.EqualTo(24000));
                Assert.That(chunk.Length, Is.EqualTo(frames / 2).Within(2));
                Assert.That(chunk.Span[chunk.Length / 2], Is.EqualTo((short)(0.4f * 32767)).Within(40));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void ToChunk_Downsampling_FiltersToneAboveTheOutputNyquist()
        {
            const int frames = 4800;
            var samples = new float[frames];
            for (var i = 0; i < frames; i++)
                samples[i] = 0.5f * MathF.Sin(2f * MathF.PI * 18000f * i / 48000f);
            var clip = Clip(1, 48000, samples);
            try
            {
                using var chunk = ClipPcm.ToChunk(clip, 24000);

                var peak = 0;
                for (var i = 100; i < chunk.Length - 100; i++)
                    peak = Math.Max(peak, Math.Abs((int)chunk.Span[i]));
                Assert.That(peak, Is.LessThan(0.05f * 32767), "an 18 kHz tone must not alias into the 12 kHz band");
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        static AudioClip Clip(int channels, int frequency, float[] interleaved)
        {
            var clip = AudioClip.Create("test", interleaved.Length / channels, channels, frequency, false);
            clip.SetData(interleaved, 0);
            return clip;
        }
    }
}
