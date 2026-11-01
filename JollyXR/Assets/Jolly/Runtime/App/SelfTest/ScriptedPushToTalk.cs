using System;
using Jolly.Contracts;

namespace Jolly.App.SelfTest
{
    /// <summary>The scene's push-to-talk plus presses the self-test makes itself, so no one has to pinch.</summary>
    public sealed class ScriptedPushToTalk : IVoiceActivation, IDisposable
    {
        readonly IVoiceActivation _inner;

        public ScriptedPushToTalk(IVoiceActivation inner)
        {
            _inner = inner;
            _inner.Pressed += RaisePressed;
            _inner.Released += RaiseReleased;
        }

        bool _scriptedHeld;

        public bool IsHeld => _scriptedHeld || _inner.IsHeld;
        public event Action Pressed;
        public event Action Released;

        public void Press()
        {
            _scriptedHeld = true;
            RaisePressed();
        }

        public void Release()
        {
            _scriptedHeld = false;
            RaiseReleased();
        }

        public void Dispose()
        {
            _inner.Pressed -= RaisePressed;
            _inner.Released -= RaiseReleased;
        }

        void RaisePressed() => Pressed?.Invoke();
        void RaiseReleased() => Released?.Invoke();
    }
}
