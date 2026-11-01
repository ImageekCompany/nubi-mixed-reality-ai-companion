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
        AudioSource _source;

        public ISpeechLevel Level => EnsureBuilt().Player;
        public bool IsPlaying => EnsureBuilt().Output.IsPlaying;

        public event Action<int> FirstSamplePlayed
        {
            add => EnsureBuilt().Output.FirstSamplePlayed += value;
            remove => EnsureBuilt().Output.FirstSamplePlayed -= value;
        }

        public UniTask Play(IUniTaskAsyncEnumerable<PcmChunk> pcm, int utteranceId, CancellationToken ct)
            => EnsureBuilt().Output.Play(pcm, utteranceId, ct);

        public UniTask PlayClip(AudioClip clip, int utteranceId, CancellationToken ct)
            => EnsureBuilt().Output.PlayClip(clip, utteranceId, ct);

        public void Flush() => EnsureBuilt().Output.Flush();

        /// <summary>Wiring may reach this component before Awake, e.g. while its GameObject is still inactive.</summary>
        (SpeechRingPlayer Player, SpeechOutput Output) EnsureBuilt()
        {
            if (_output == null)
            {
                var player = new SpeechRingPlayer(SpeechSampleRate, AudioSettings.outputSampleRate, SpeechSampleRate * RingSeconds);
                _output = new SpeechOutput(player);
                Volatile.Write(ref _player, player);
            }
            return (_player, _output);
        }

        void Awake()
        {
            EnsureBuilt().Player.SetOutputSampleRate(AudioSettings.outputSampleRate);
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

            _source = GetComponent<AudioSource>();
            _source.clip = null;
            _source.loop = true;
            _source.playOnAwake = false;
        }

        void OnEnable() => EnsureSourcePlaying();

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

        // An audio reset or device change stops every source; without a running source OnAudioFilterRead never runs
        // and every Play would wait forever.
        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            _player.SetOutputSampleRate(AudioSettings.outputSampleRate);
            EnsureSourcePlaying();
        }

        void EnsureSourcePlaying()
        {
            if (isActiveAndEnabled && !_source.isPlaying)
                _source.Play();
        }
    }
}
