using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>IVoiceActivation driven by the test; a repeated Press or Release is ignored, like a held button.</summary>
    public sealed class FakeVoiceActivation : IVoiceActivation
    {
        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        public void Press()
        {
            if (IsHeld)
                return;
            IsHeld = true;
            Pressed?.Invoke();
        }

        public void Release()
        {
            if (!IsHeld)
                return;
            IsHeld = false;
            Released?.Invoke();
        }
    }
}
