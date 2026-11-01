using System;
using Jolly.Contracts;

namespace Jolly.Core.Conversation
{
    /// <summary>
    /// The microphone for push-to-talk, started early while a pinch waits to become a tap or a hold (ADR-0044), so a
    /// hold loses none of the words said during the tap time. The waiting press starts a local capture session; a hold
    /// hands it, with everything heard so far, to the turn's <see cref="Start"/>; a tap, or a release that no turn
    /// claimed (offline, a persona switch), disposes it unheard: nothing reaches ASR or the model. It does not pre-roll
    /// while <c>canPreRoll</c> is false: while the character speaks the pre-roll would hold its own voice (no verified
    /// echo cancellation, research/voice.md §6), and while a turn listens a second start would abort its session.
    /// Main thread only.
    /// </summary>
    public sealed class PreRollCapture : IAudioCapture, IDisposable
    {
        readonly IAudioCapture _inner;
        readonly TapHoldClassifier _pinch;
        readonly int _sampleRate;
        readonly Func<bool> _canPreRoll;
        IAudioCaptureSession _primed;

        public PreRollCapture(IAudioCapture inner, TapHoldClassifier pinch, int sampleRate, Func<bool> canPreRoll)
        {
            _inner = inner;
            _pinch = pinch;
            _sampleRate = sampleRate;
            _canPreRoll = canPreRoll;
            _pinch.Began += Prime;
            _pinch.Tapped += Discard;
            _pinch.Released += Discard;
        }

        public bool IsAvailable => _inner.IsAvailable;
        public bool IsPrimed => _primed != null;

        /// <summary>The primed session when it has the asked rate, else a new one.</summary>
        public IAudioCaptureSession Start(int targetSampleRate)
        {
            if (_primed != null && targetSampleRate == _sampleRate)
            {
                var session = _primed;
                _primed = null;
                return session;
            }
            Discard();
            return _inner.Start(targetSampleRate);
        }

        public void Dispose()
        {
            _pinch.Began -= Prime;
            _pinch.Tapped -= Discard;
            _pinch.Released -= Discard;
            Discard();
        }

        void Prime()
        {
            Discard();
            if (_inner.IsAvailable && _canPreRoll())
                _primed = _inner.Start(_sampleRate);
        }

        void Discard()
        {
            _primed?.Dispose();
            _primed = null;
        }
    }
}
