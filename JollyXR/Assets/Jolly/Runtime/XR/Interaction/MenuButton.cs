using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// The app menu button (ADR-0042): ☰ on the left Touch controller (OpenXR <c>/user/hand/left/input/menu/click</c>,
    /// which Horizon OS leaves to the app; the right hand's Meta button is the system's), or the left-hand palm pinch that
    /// Horizon OS reports to the app as <see cref="MetaAimFlags.MenuPressed"/> through the Meta hand-tracking aim
    /// extension. Bound by layout name like <c>ControllerPushToTalk</c> (ADR-0021).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuButton : MonoBehaviour
    {
        static readonly string[] TouchLayouts = { "OculusTouchController", "QuestTouchPlusController", "QuestProTouchController" };
        const int MenuPressedFlag = (int)MetaAimFlags.MenuPressed;

        InputAction _controller;
        InputAction _handAimFlags;
        bool _handPressed;

        public event Action Pressed;

        void Awake()
        {
            _controller = new InputAction("Menu", InputActionType.Button);
            foreach (var layout in TouchLayouts)
                _controller.AddBinding("<" + layout + ">{LeftHand}/menu");
            _handAimFlags = new InputAction("Menu Hand", InputActionType.PassThrough, "<MetaAimHand>{LeftHand}/aimFlags");
        }

        void OnEnable()
        {
            _controller.Enable();
            _handAimFlags.Enable();
        }

        void Update()
        {
            if (_controller.WasPressedThisFrame())
                Pressed?.Invoke();
            // Edge-triggered: the flag stays set for as long as the pinch is held.
            var handPressed = (_handAimFlags.ReadValue<int>() & MenuPressedFlag) != 0;
            if (handPressed && !_handPressed)
                Pressed?.Invoke();
            _handPressed = handPressed;
        }

        void OnDisable()
        {
            _controller.Disable();
            _handAimFlags.Disable();
            _handPressed = false;
        }

        void OnDestroy()
        {
            _controller.Dispose();
            _handAimFlags.Dispose();
        }
    }
}
