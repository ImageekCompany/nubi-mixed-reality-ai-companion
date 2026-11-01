using System;

namespace Jolly.Audio.Capture
{
    /// <summary>
    /// A looping capture ring filled by the OS, as <c>UnityEngine.Microphone</c> exposes it. The seam that keeps
    /// <see cref="MicrophoneCaptureSession"/> testable without a microphone. Main thread only.
    /// </summary>
    public interface IMicrophoneDevice
    {
        bool IsAvailable { get; }
        /// <summary>Starts looping capture with the write head at 0. False when the OS refused.</summary>
        bool Start();
        /// <summary>Valid after a successful <see cref="Start"/>.</summary>
        int SampleRate { get; }
        /// <summary>Ring length in samples; valid after a successful <see cref="Start"/>.</summary>
        int RingLength { get; }
        /// <summary>Write head in [0, RingLength).</summary>
        int Position { get; }
        /// <summary>Copies destination.Length samples starting at offset; offset + length must not pass the ring end.</summary>
        void Read(int offset, Span<float> destination);
        void Stop();
    }
}
