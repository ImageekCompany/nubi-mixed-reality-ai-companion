using System;
using Jolly.Contracts;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// Push-to-talk pressed only by the self-test. The pinch detector is left out on purpose: with nobody wearing the
    /// headset, hand tracking produced phantom press/release pairs that barged into the scripted turns (device run,
    /// 2026-11-01).
    /// </summary>
    public sealed class ScriptedPushToTalk : IVoiceActivation
    {
        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        public void Press()
        {
            IsHeld = true;
            Pressed?.Invoke();
        }

        public void Release()
        {
            IsHeld = false;
            Released?.Invoke();
        }
    }
}
