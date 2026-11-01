using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Jolly.XR.Voice
{
    /// <summary>
    /// Push-to-talk from a thumb-index pinch (either hand or the dominant one, per IUserPreferences) or a controller
    /// button. A pinch starts talking only when that hand's ray hovers nothing (so pinching UI or poking Nubi does not
    /// talk) and the palm does not face the head (the Horizon OS system-menu gesture).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PinchPushToTalk : MonoBehaviour, IVoiceActivation
    {
        // cos 53°: a palm turned toward the face, not one held sideways while pinching.
        const float PalmTowardHeadMinCos = 0.6f;

        [SerializeField] XRBaseInteractor leftHandRay;
        [SerializeField] XRBaseInteractor rightHandRay;
        [SerializeField] InputActionReference controllerTalk;
        [SerializeField] Handedness dominantHand = Handedness.Right;

        readonly GatedPinch _left = new();
        readonly GatedPinch _right = new();
        IHandSource _hands;
        Transform _head;
        IUserPreferences _preferences;

        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        public void Construct(IHandSource hands, Transform head, IUserPreferences preferences)
        {
            _hands = hands;
            _head = head;
            _preferences = preferences;
        }

        void Update()
        {
            if (_hands == null)
                return;
            var eitherHand = _preferences == null || _preferences.Current.PttEitherHand;
            var held = Track(Handedness.Left, _left, leftHandRay, eitherHand)
                       | Track(Handedness.Right, _right, rightHandRay, eitherHand)
                       | ControllerHeld();
            SetHeld(held);
        }

        void OnDisable()
        {
            _left.Reset();
            _right.Reset();
            SetHeld(false);
        }

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

        bool Track(Handedness side, GatedPinch pinch, XRBaseInteractor ray, bool eitherHand)
        {
            if (!eitherHand && side != dominantHand)
            {
                pinch.Reset();
                return false;
            }
            if (!_hands.TryGetHand(side, out var hand))
                return pinch.Update(null, gateOpen: false);

            var gateOpen = !IsHovering(ray) && !HandGestures.PalmFaces(hand, _head.position, PalmTowardHeadMinCos);
            return pinch.Update(HandGestures.PinchDistance(hand), gateOpen);
        }

        bool ControllerHeld() => controllerTalk != null && controllerTalk.action != null && controllerTalk.action.IsPressed();

        static bool IsHovering(XRBaseInteractor ray)
        {
            if (ray == null)
                return false;
            if (ray.hasHover)
                return true;
            return ray is NearFarInteractor nearFar && nearFar.TryGetUIModel(out var model) && model.currentRaycast.isValid;
        }
    }
}
