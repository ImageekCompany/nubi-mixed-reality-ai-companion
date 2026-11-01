using System;
using Jolly.Audio.Capture;

namespace Jolly.Tests.EditMode.Audio
{
    /// <summary>Scripted capture ring: tests write samples with <see cref="Produce"/> and the head advances.</summary>
    sealed class FakeMicrophoneDevice : IMicrophoneDevice
    {
        readonly float[] _ring;

        public FakeMicrophoneDevice(int sampleRate, int ringLength)
        {
            SampleRate = sampleRate;
            _ring = new float[ringLength];
        }

        public bool IsAvailable { get; set; } = true;
        public bool RefuseStart { get; set; }
        public bool IsRecording { get; private set; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public int SampleRate { get; }
        public int RingLength => _ring.Length;
        public int Position { get; private set; }

        public bool Start()
        {
            if (RefuseStart)
                return false;
            Starts++;
            IsRecording = true;
            Position = 0;
            return true;
        }

        public void Read(int offset, Span<float> destination)
        {
            if (offset + destination.Length > _ring.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "read passes the ring end");
            new ReadOnlySpan<float>(_ring, offset, destination.Length).CopyTo(destination);
        }

        public void Stop()
        {
            Stops++;
            IsRecording = false;
        }

        public void Produce(ReadOnlySpan<float> samples)
        {
            foreach (var s in samples)
            {
                _ring[Position] = s;
                Position = (Position + 1) % _ring.Length;
            }
        }

        public static float[] Ramp(int start, int count)
        {
            var samples = new float[count];
            for (var i = 0; i < count; i++)
                samples[i] = (start + i) / 32767f;
            return samples;
        }
    }
}
