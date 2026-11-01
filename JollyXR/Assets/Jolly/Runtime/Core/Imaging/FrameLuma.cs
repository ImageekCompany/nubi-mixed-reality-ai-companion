using System;

namespace Jolly.Core.Imaging
{
    /// <summary>
    /// Mean Rec. 601 luma (0–255) of an RGBA32 frame, the dark-room test shared by the vision tools and the self-test.
    /// Runs on the frame already downsampled for vision, on the capture path, so it samples at most
    /// <see cref="MaxSamples"/> pixels evenly instead of all of them and allocates nothing.
    /// </summary>
    public static class FrameLuma
    {
        public const int MaxSamples = 16384;

        /// <summary>NaN for an empty frame.</summary>
        public static double Mean(ReadOnlySpan<byte> rgba32)
        {
            var pixels = rgba32.Length / 4;
            if (pixels == 0)
                return double.NaN;
            var step = (pixels + MaxSamples - 1) / MaxSamples;
            long sum = 0;
            var count = 0;
            for (var i = 0; i < pixels; i += step)
            {
                var p = i * 4;
                sum += 299 * rgba32[p] + 587 * rgba32[p + 1] + 114 * rgba32[p + 2];
                count++;
            }
            return sum / (1000.0 * count);
        }
    }
}
