using System;
using Jolly.Contracts;

namespace Jolly.Testing
{
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
