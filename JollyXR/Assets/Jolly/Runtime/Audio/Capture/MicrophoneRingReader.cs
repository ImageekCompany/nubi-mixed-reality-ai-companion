using System;

namespace Jolly.Audio.Capture
{
    /// <summary>
    /// Read cursor over a device's looping ring. If the caller falls a whole ring behind, the lapped audio is lost and
    /// cannot be detected; the ring is sized (seconds) to make that impossible at any sane frame rate.
    /// </summary>
    sealed class MicrophoneRingReader
    {
        int _cursor;

        public void Reset() => _cursor = 0;

        /// <summary>Samples written since the last read.</summary>
        public int Available(IMicrophoneDevice device) => (device.Position - _cursor + device.RingLength) % device.RingLength;

        /// <summary>Copies up to destination.Length new samples and returns the count.</summary>
        public int Read(IMicrophoneDevice device, Span<float> destination)
        {
            var ring = device.RingLength;
            var count = Math.Min(Available(device), destination.Length);
            if (count == 0)
                return 0;

            var first = Math.Min(count, ring - _cursor);
            device.Read(_cursor, destination.Slice(0, first));
            if (count > first)
                device.Read(0, destination.Slice(first, count - first));
            _cursor = (_cursor + count) % ring;
            return count;
        }
    }
}
