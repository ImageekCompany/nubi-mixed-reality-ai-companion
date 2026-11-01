using System;
using System.Collections.Generic;
using Jolly.Audio.Capture;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The self-test's microphone: the real device unless a clip is armed, in which case the next capture hears the clip
    /// in real time (then silence), through the same ring, resampler and chunker as the headset microphone.
    /// <see cref="ArmSilence"/> captures silence. Main thread only.
    /// </summary>
    public sealed class ClipMicrophone : IMicrophoneDevice
    {
        const int RingSeconds = 2;

        readonly IMicrophoneDevice _real;
        readonly IClock _clock;
        readonly Dictionary<AudioClip, float[]> _converted = new();
        float[] _clip;
        int _clipRate;
        float[] _ring;
        bool _armed;
        bool _playing;
        double _startedAt;
        long _filled;

        public ClipMicrophone(IMicrophoneDevice real, IClock clock)
        {
            _real = real;
            _clock = clock;
        }

        /// <summary>
        /// Converts <paramref name="clip"/> to mono samples now. The session prepares its clips at launch: converting at
        /// <see cref="Arm"/> allocated ~0.5 MB inside every scripted turn and counted against its GC budget (ADR-0023).
        /// </summary>
        public void Prepare(AudioClip clip)
        {
            if (clip == null || _converted.ContainsKey(clip))
                return;
            var interleaved = new float[clip.samples * clip.channels];
            clip.GetData(interleaved, 0);
            var mono = new float[clip.samples];
            for (var i = 0; i < mono.Length; i++)
                mono[i] = interleaved[i * clip.channels];
            _converted[clip] = mono;
            _ring ??= new float[clip.frequency * RingSeconds];
        }

        /// <summary>Mono samples of a Decompress On Load clip; heard from the next <see cref="Start"/>.</summary>
        public void Arm(AudioClip clip)
        {
            Prepare(clip);
            _clip = _converted[clip];
            _clipRate = clip.frequency;
            _armed = true;
        }

        public void ArmSilence()
        {
            _clip = Array.Empty<float>();
            _clipRate = 24000;
            _armed = true;
        }

        public bool IsAvailable => _armed || _real.IsAvailable;
        public int SampleRate => _playing ? _clipRate : _real.SampleRate;
        public int RingLength => _playing ? _ring.Length : _real.RingLength;

        public int Position
        {
            get
            {
                if (!_playing)
                    return _real.Position;
                Fill();
                return (int)(_filled % _ring.Length);
            }
        }

        public bool Start()
        {
            if (!_armed)
                return _real.Start();
            _armed = false;
            _playing = true;
            if (_ring == null || _ring.Length != _clipRate * RingSeconds)
                _ring = new float[_clipRate * RingSeconds];
            _startedAt = _clock.Now;
            _filled = 0;
            return true;
        }

        public void Read(int offset, Span<float> destination)
        {
            if (_playing)
                _ring.AsSpan(offset, destination.Length).CopyTo(destination);
            else
                _real.Read(offset, destination);
        }

        public void Stop()
        {
            if (_playing)
                _playing = false;
            else
                _real.Stop();
        }

        void Fill()
        {
            var due = (long)((_clock.Now - _startedAt) * _clipRate);
            for (; _filled < due; _filled++)
                _ring[_filled % _ring.Length] = _filled < _clip.Length ? _clip[_filled] : 0f;
        }
    }
}
