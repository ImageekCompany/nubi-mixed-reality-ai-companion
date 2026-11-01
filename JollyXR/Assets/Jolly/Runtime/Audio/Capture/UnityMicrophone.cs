using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Jolly.Audio.Capture
{
    /// <summary>
    /// <see cref="IMicrophoneDevice"/> over <c>UnityEngine.Microphone</c> with the default device. Quest 3 reports caps
    /// 16000–48000 and delivers a 48 kHz mono clip (device spike §1a), so the ring runs at the device maximum and
    /// <see cref="Jolly.Audio.Resampling.Resampler"/> converts. Main thread only.
    /// </summary>
    public sealed class UnityMicrophone : IMicrophoneDevice
    {
        const int RingSeconds = 2;
        const int FallbackRate = 48000;

        AudioClip _clip;

        public bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
                    return false;
#endif
                return Microphone.devices.Length > 0;
            }
        }

        public int SampleRate { get; private set; }
        public int RingLength { get; private set; }
        public int Position => _clip == null ? 0 : Math.Max(0, Microphone.GetPosition(null));

        public bool Start()
        {
            Stop();
            // GetDeviceCaps reports 0/0 when any rate is supported.
            Microphone.GetDeviceCaps(null, out _, out var maxRate);
            var rate = maxRate > 0 ? maxRate : FallbackRate;
            var clip = Microphone.Start(null, true, RingSeconds, rate);
            if (clip == null)
                return false;

            _clip = clip;
            SampleRate = clip.frequency;
            RingLength = clip.samples;
            return true;
        }

        public void Read(int offset, Span<float> destination)
        {
            if (_clip != null && !destination.IsEmpty)
                _clip.GetData(destination, offset);
        }

        public void Stop()
        {
            if (_clip == null)
                return;
            Microphone.End(null);
            if (Application.isPlaying)
                Object.Destroy(_clip);
            else
                Object.DestroyImmediate(_clip);
            _clip = null;
        }
    }
}
