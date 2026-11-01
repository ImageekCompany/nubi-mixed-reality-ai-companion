using System;
using Cysharp.Threading.Tasks;
using Jolly.Audio.Resampling;
using Jolly.Contracts;

namespace Jolly.Audio.Capture
{
    /// <summary>
    /// Packs float samples into fixed-size pooled <see cref="PcmChunk"/>s and hands each full one to the writer.
    /// Capture times are derived from the sample count, so they stay exact however the input is split.
    /// </summary>
    sealed class PcmChunker
    {
        readonly ChannelWriter<PcmChunk> _writer;
        readonly int _sampleRate;
        readonly int _chunkSamples;
        PcmChunk _current;
        int _filled;
        long _emitted;
        double _startTime = double.NaN;

        public PcmChunker(ChannelWriter<PcmChunk> writer, int sampleRate, int chunkSamples)
        {
            _writer = writer;
            _sampleRate = sampleRate;
            _chunkSamples = chunkSamples;
        }

        /// <summary>RMS 0..1 of the last emitted chunk.</summary>
        public float Level { get; private set; }

        /// <summary>Sets the clock time of the first sample; only the first call counts.</summary>
        public void SetStartTimeOnce(double time)
        {
            if (double.IsNaN(_startTime))
                _startTime = time;
        }

        public void Append(ReadOnlySpan<float> samples)
        {
            while (!samples.IsEmpty)
            {
                _current ??= PcmChunk.Rent(_chunkSamples, _sampleRate, CurrentTime());
                var count = Math.Min(samples.Length, _chunkSamples - _filled);
                Pcm16.FromFloat(samples.Slice(0, count), new Span<short>(_current.Buffer, _filled, count));
                _filled += count;
                samples = samples.Slice(count);
                if (_filled == _chunkSamples)
                    Emit(_filled);
            }
        }

        /// <summary>Emits the partial chunk, if any.</summary>
        public void Flush()
        {
            if (_current != null && _filled > 0)
                Emit(_filled);
        }

        /// <summary>Returns the partial chunk to the pool without emitting it.</summary>
        public void Discard()
        {
            _current?.Dispose();
            _current = null;
            _filled = 0;
        }

        void Emit(int length)
        {
            var chunk = length == _chunkSamples ? _current : Shrink(_current, length);
            Level = Pcm16.Rms(chunk.Span);
            _emitted += length;
            _current = null;
            _filled = 0;
            if (!_writer.TryWrite(chunk))
                chunk.Dispose();
        }

        PcmChunk Shrink(PcmChunk full, int length)
        {
            var partial = PcmChunk.Rent(length, _sampleRate, full.CaptureTime);
            full.Span.Slice(0, length).CopyTo(partial.Buffer);
            full.Dispose();
            return partial;
        }

        double CurrentTime() => (double.IsNaN(_startTime) ? 0 : _startTime) + (double)_emitted / _sampleRate;
    }
}
