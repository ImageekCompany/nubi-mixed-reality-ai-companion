using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using Jolly.XR.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Voice
{
    /// <summary>
    /// Push-to-talk from holding A or X on a Touch controller (ADR-0021). The buttons are bound to the Touch layouts by
    /// name, never by usage and never through the XRI Starter actions: a hand can surface as a controller (the Hand
    /// Interaction profile's XRController, a runtime that reports a pinch as a trigger, or the KHR simple controller,
    /// whose select carries the PrimaryButton usage), and a press from any of those would skip the gating that
    /// <see cref="PinchPushToTalk"/> applies to a pinch. Trigger (XRI Activate, UI press) and grip (XRI Select) stay
    /// free. Like XRInputModalityManager, a side whose hand is tracked is in hand mode, so its button is ignored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControllerPushToTalk : MonoBehaviour, IVoiceActivation
    {
        InputAction _left;
        InputAction _right;
        IHandSource _hands;

        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        /// <param name="hands">Null treats both hands as untracked.</param>
        public void Construct(IHandSource hands) => _hands = hands;

        void Awake()
        {
            _left = TouchBindings.Button("Talk Left", "{LeftHand}", "primaryButton");
            _right = TouchBindings.Button("Talk Right", "{RightHand}", "primaryButton");
        }

        void OnEnable()
        {
            _left.Enable();
            _right.Enable();
        }

        void Update() => SetHeld(Held(_left, Handedness.Left) || Held(_right, Handedness.Right));

        void OnDisable()
        {
            _left.Disable();
            _right.Disable();
            SetHeld(false);
        }

        void OnDestroy()
        {
            _left.Dispose();
            _right.Dispose();
        }

        bool Held(InputAction action, Handedness side) =>
            action.IsPressed() && (_hands == null || !_hands.TryGetHand(side, out _));

        void SetHeld(bool held)
        {
            if (held == IsHeld)
                return;
            IsHeld = held;
            if (held)
                Pressed?.Invoke();
            else
                Released?.Invoke();
        }
    }
}
