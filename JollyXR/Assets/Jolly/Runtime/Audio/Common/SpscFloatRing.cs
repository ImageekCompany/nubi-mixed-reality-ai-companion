using System;
using System.Threading;

namespace Jolly.Audio.Common
{
    /// <summary>
    /// Lock-free, allocation-free sample ring for exactly one producer thread and one consumer thread
    /// (e.g. the main thread writes decoded PCM, OnAudioFilterRead reads it). <see cref="Write"/> belongs to the
    /// producer; <see cref="Read"/> and <see cref="Clear"/> belong to the consumer.
    /// </summary>
    public sealed class SpscFloatRing
    {
        readonly float[] _buffer;
        long _writePosition;
        long _readPosition;

        public SpscFloatRing(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            _buffer = new float[capacity];
        }

        public int Capacity => _buffer.Length;

        /// <summary>A snapshot: the other thread may change it immediately.</summary>
        public int Count => (int)(Volatile.Read(ref _writePosition) - Volatile.Read(ref _readPosition));

        public int FreeSpace => Capacity - Count;

        /// <summary>Producer only. Copies as many samples as fit and returns that count; never overwrites unread data.</summary>
        public int Write(ReadOnlySpan<float> samples)
        {
            var write = _writePosition;
            var free = _buffer.Length - (int)(write - Volatile.Read(ref _readPosition));
            var count = Math.Min(free, samples.Length);
            if (count == 0)
                return 0;

            var start = (int)(write % _buffer.Length);
            var firstPart = Math.Min(count, _buffer.Length - start);
            samples.Slice(0, firstPart).CopyTo(new Span<float>(_buffer, start, firstPart));
            samples.Slice(firstPart, count - firstPart).CopyTo(new Span<float>(_buffer, 0, count - firstPart));

            Volatile.Write(ref _writePosition, write + count);
            return count;
        }

        /// <summary>Consumer only. Copies up to destination.Length samples and returns the count; on underrun the
        /// remainder of destination is left untouched, so the caller decides between silence and a fade.</summary>
        public int Read(Span<float> destination)
        {
            var read = _readPosition;
            var available = (int)(Volatile.Read(ref _writePosition) - read);
            var count = Math.Min(available, destination.Length);
            if (count == 0)
                return 0;

            var start = (int)(read % _buffer.Length);
            var firstPart = Math.Min(count, _buffer.Length - start);
            new ReadOnlySpan<float>(_buffer, start, firstPart).CopyTo(destination);
            new ReadOnlySpan<float>(_buffer, 0, count - firstPart).CopyTo(destination.Slice(firstPart));

            Volatile.Write(ref _readPosition, read + count);
            return count;
        }

        /// <summary>Consumer only. Drops every queued sample; a concurrent Write may land just after.</summary>
        public void Clear() => Volatile.Write(ref _readPosition, Volatile.Read(ref _writePosition));
    }
}
