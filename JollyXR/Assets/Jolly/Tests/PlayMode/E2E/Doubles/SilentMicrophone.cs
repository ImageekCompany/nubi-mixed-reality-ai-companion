using System;
using Jolly.Audio.Capture;
using UnityEngine;

namespace Jolly.Tests.PlayMode.E2E.Doubles
{
    /// <summary>The headset microphone at its edge: a 24 kHz ring that fills with silence in real time.</summary>
    public sealed class SilentMicrophone : IMicrophoneDevice
    {
        const int Rate = 24000;
        double _startedAt;
        bool _running;

        public bool IsAvailable => true;
        public int SampleRate => Rate;
        public int RingLength => Rate * 2;
        public int Position => _running ? (int)((Time.realtimeSinceStartupAsDouble - _startedAt) * Rate) % RingLength : 0;

        public bool Start()
        {
            _startedAt = Time.realtimeSinceStartupAsDouble;
            _running = true;
            return true;
        }

        public void Read(int offset, Span<float> destination) => destination.Clear();

        public void Stop() => _running = false;
    }
}
