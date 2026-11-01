using System;
using System.Threading;
using Jolly.Audio.Common;

namespace Jolly.Audio.Output
{
    /// <summary>Loudness of what the speech output last rendered; readable from any thread.</summary>
    public interface ISpeechLevel
    {
        /// <summary>RMS 0..1 of the last audio block.</summary>
        float Level { get; }
    }

    /// <summary>
    /// The audio-thread half of the speech output: pulls source-rate samples from an SPSC ring, resamples them linearly
    /// to the output rate and writes every channel. Positions are counted in source samples so the main thread can tell
    /// which utterance the DSP has reached. <see cref="Render"/> runs on the audio thread and never allocates in steady
    /// state; everything else is main-thread only.
    /// </summary>
    public sealed class SpeechRingPlayer : ISpeechLevel
    {
        const double FadeSeconds = 0.020;

        readonly SpscFloatRing _ring;
        float[] _scratch = new float[4096];
        int _outputRate;
        long _written;
        long _consumed;
        long _discardUntil;
        long _phase;
        float _previous;
        float _next;
        float _level;

        public SpeechRingPlayer(int sourceSampleRate, int outputSampleRate, int capacitySamples)
        {
            if (sourceSampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceSampleRate));
            SourceSampleRate = sourceSampleRate;
            SetOutputSampleRate(outputSampleRate);
            _ring = new SpscFloatRing(capacitySamples);
        }

        public int SourceSampleRate { get; }
        public int FreeSpace => _ring.FreeSpace;
        public long Written => _written;
        public long Consumed => Volatile.Read(ref _consumed);
        public float Level => Volatile.Read(ref _level);

        public void SetOutputSampleRate(int outputSampleRate)
        {
            if (outputSampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(outputSampleRate));
            Volatile.Write(ref _outputRate, outputSampleRate);
        }

        public int Write(ReadOnlySpan<float> samples)
        {
            var count = _ring.Write(samples);
            _written += count;
            return count;
        }

        /// <summary>Everything written so far is faded out (20 ms) and dropped by the next Render; later writes play.</summary>
        public void DiscardQueued() => Volatile.Write(ref _discardUntil, _written);

        public void Render(float[] data, int channels)
        {
            var frames = data.Length / channels;
            var outputRate = Volatile.Read(ref _outputRate);
            _phase %= outputRate;
            var sumOfSquares = 0f;
            var offset = 0;

            var discardUntil = Volatile.Read(ref _discardUntil);
            if (discardUntil > _consumed)
            {
                var fadeFrames = Math.Min(frames, (int)(outputRate * FadeSeconds));
                sumOfSquares += RenderFrames(data, 0, fadeFrames, channels, outputRate, discardUntil - _consumed, true);
                SkipTo(discardUntil);
                _previous = _next = 0f;
                _phase = 0;
                offset = fadeFrames;
            }
            sumOfSquares += RenderFrames(data, offset, frames - offset, channels, outputRate, long.MaxValue, false);

            Volatile.Write(ref _consumed, _consumed);
            Volatile.Write(ref _level, frames > 0 ? MathF.Sqrt(sumOfSquares / frames) : 0f);
        }

        float RenderFrames(float[] data, int firstFrame, int count, int channels, int outputRate, long readLimit, bool fadeOut)
        {
            if (count <= 0)
                return 0f;

            var needed = (int)Math.Min((_phase + (long)count * SourceSampleRate) / outputRate, readLimit);
            if (_scratch.Length < needed)
                _scratch = new float[needed];
            var got = needed > 0 ? _ring.Read(new Span<float>(_scratch, 0, needed)) : 0;
            _consumed += got;

            var index = 0;
            var sumOfSquares = 0f;
            for (var frame = 0; frame < count; frame++)
            {
                _phase += SourceSampleRate;
                while (_phase >= outputRate)
                {
                    _phase -= outputRate;
                    _previous = _next;
                    _next = index < got ? _scratch[index++] : 0f;
                }

                var sample = _previous + (_next - _previous) * ((float)_phase / outputRate);
                if (fadeOut)
                    sample *= 1f - (frame + 1f) / count;
                sumOfSquares += sample * sample;

                var at = (firstFrame + frame) * channels;
                for (var c = 0; c < channels; c++)
                    data[at + c] = sample;
            }
            return sumOfSquares;
        }

        void SkipTo(long position)
        {
            while (_consumed < position)
            {
                var count = (int)Math.Min(_scratch.Length, position - _consumed);
                var got = _ring.Read(new Span<float>(_scratch, 0, count));
                if (got == 0)
                    return;
                _consumed += got;
            }
        }
    }
}
