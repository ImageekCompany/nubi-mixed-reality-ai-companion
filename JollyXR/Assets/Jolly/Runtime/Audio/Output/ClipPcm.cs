using System;
using System.Buffers;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Audio.Output
{
    /// <summary>
    /// Converts a baked AudioClip (Decompress On Load, so GetData works) to one mono s16 chunk at the speech rate,
    /// down-mixing and linearly resampling when needed. Main thread only.
    /// </summary>
    public static class ClipPcm
    {
        public static PcmChunk ToChunk(AudioClip clip, int targetSampleRate)
        {
            var channels = clip.channels;
            var frames = clip.samples;
            var interleaved = ArrayPool<float>.Shared.Rent(frames * channels);
            try
            {
                if (!clip.GetData(interleaved, 0))
                    throw new ArgumentException($"Cannot read samples of clip '{clip.name}'; import it as Decompress On Load.", nameof(clip));

                var length = (int)((long)frames * targetSampleRate / clip.frequency);
                var chunk = PcmChunk.Rent(length, targetSampleRate);
                var ratio = (double)clip.frequency / targetSampleRate;
                for (var i = 0; i < length; i++)
                {
                    var position = i * ratio;
                    var index = (int)position;
                    var a = Mono(interleaved, index, channels);
                    var b = index + 1 < frames ? Mono(interleaved, index + 1, channels) : a;
                    var sample = a + (b - a) * (float)(position - index);
                    chunk.Buffer[i] = (short)Math.Clamp((int)(sample * 32767f), short.MinValue, short.MaxValue);
                }
                return chunk;
            }
            finally
            {
                ArrayPool<float>.Shared.Return(interleaved);
            }
        }

        static float Mono(float[] interleaved, int frame, int channels)
        {
            var sum = 0f;
            var at = frame * channels;
            for (var c = 0; c < channels; c++)
                sum += interleaved[at + c];
            return sum / channels;
        }
    }
}
