using System;

namespace Jolly.Audio.Resampling
{
    /// <summary>Float [-1, 1] ↔ signed 16-bit PCM. Symmetric scale (±32767), so -1 and 1 map to equal magnitudes.</summary>
    public static class Pcm16
    {
        const float Scale = 32767f;

        public static void FromFloat(ReadOnlySpan<float> source, Span<short> destination)
        {
            if (destination.Length < source.Length)
                throw new ArgumentException("Destination is shorter than source.", nameof(destination));

            for (var i = 0; i < source.Length; i++)
            {
                var s = source[i] * Scale;
                destination[i] = s >= Scale ? (short)32767 : s <= -Scale ? (short)-32767 : (short)MathF.Round(s);
            }
        }

        /// <summary>Root mean square in 0..1.</summary>
        public static float Rms(ReadOnlySpan<short> samples)
        {
            if (samples.Length == 0)
                return 0f;

            double sum = 0;
            foreach (var s in samples)
                sum += (double)s * s;
            return (float)(Math.Sqrt(sum / samples.Length) / Scale);
        }
    }
}
