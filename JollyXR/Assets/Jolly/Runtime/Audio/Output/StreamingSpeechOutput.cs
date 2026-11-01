using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Audio.Output
{
    /// <summary>
    /// Clip-less AudioSource whose <c>OnAudioFilterRead</c> plays the speech ring (architecture §11 Audio output):
    /// Flush silences within one DSP buffer and FirstSamplePlayed follows what the mixer actually took. Put the uLipSync
    /// component after this one on the same GameObject so it analyses the played samples.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class StreamingSpeechOutput : MonoBehaviour, ISpeechOutput
    {
        const int SpeechSampleRate = 24000;
        const int RingSeconds = 10;

        SpeechRingPlayer _player;
        SpeechOutput _output;

        public ISpeechLevel Level => _player;
        public bool IsPlaying => _output.IsPlaying;

        public event Action<int> FirstSamplePlayed
        {
            add => _output.FirstSamplePlayed += value;
            remove => _output.FirstSamplePlayed -= value;
        }

        public UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct)
            => _output.Play(pcm, utteranceId, ct);

        public UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct) => _output.PlayClip(clip, utteranceId, ct);

        public void Flush() => _output.Flush();

        void Awake()
        {
            _player = new SpeechRingPlayer(SpeechSampleRate, AudioSettings.outputSampleRate, SpeechSampleRate * RingSeconds);
            _output = new SpeechOutput(_player);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            var source = GetComponent<AudioSource>();
            source.clip = null;
            source.loop = true;
            source.playOnAwake = false;
            source.Play();
        }

        void Update() => _output.Tick();

        void OnAudioFilterRead(float[] data, int channels)
        {
            var player = Volatile.Read(ref _player);
            if (player != null)
                player.Render(data, channels);
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            _output?.Dispose();
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged) => _player.SetOutputSampleRate(AudioSettings.outputSampleRate);
    }
}
