using System;
using System.Buffers;
using Jolly.Audio.Resampling;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Audio.Output
{
    /// <summary>
    /// Converts a baked AudioClip (Decompress On Load, so GetData works) to one mono s16 chunk at the speech rate,
    /// down-mixing and, for an off-rate clip, resampling with the capture path's anti-aliased <see cref="Resampler"/>.
    /// Main thread only.
    /// </summary>
    public static class ClipPcm
    {
        // Clip-sized scratch kept between canned lines; ArrayPool.Shared reallocated it on Quest (ADR-0023).
        static readonly ArrayPool<float> Scratch = ArrayPool<float>.Create();

        public static PcmChunk ToChunk(AudioClip clip, int targetSampleRate)
        {
            var channels = clip.channels;
            var frames = clip.samples;
            var pool = Scratch;
            var interleaved = pool.Rent(frames * channels);
            float[] resampled = null;
            try
            {
                // The rented array may be longer than the clip; Unity warns on every oversized read.
                if (!clip.GetData(interleaved.AsSpan(0, frames * channels), 0))
                    throw new ArgumentException($"Cannot read samples of clip '{clip.name}'; import it as Decompress On Load.", nameof(clip));

                DownMixInPlace(interleaved, frames, channels);
                var resampler = new Resampler(clip.frequency, targetSampleRate);
                resampled = pool.Rent(resampler.MaxOutputLength(frames));
                var length = resampler.Process(interleaved.AsSpan(0, frames), resampled);

                var chunk = PcmChunk.Rent(length, targetSampleRate);
                Pcm16.FromFloat(resampled.AsSpan(0, length), chunk.Buffer);
                return chunk;
            }
            finally
            {
                pool.Return(interleaved);
                if (resampled != null)
                    pool.Return(resampled);
            }
        }

        // Frame i is written to index i, which never passes the frame's own interleaved start (i * channels).
        static void DownMixInPlace(float[] interleaved, int frames, int channels)
        {
            if (channels == 1)
                return;
            for (var frame = 0; frame < frames; frame++)
            {
                var sum = 0f;
                var at = frame * channels;
                for (var c = 0; c < channels; c++)
                    sum += interleaved[at + c];
                interleaved[frame] = sum / channels;
            }
        }
    }
}
