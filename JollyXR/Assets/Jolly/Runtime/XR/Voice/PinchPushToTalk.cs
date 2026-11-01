using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using Jolly.XR.Interaction;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Jolly.XR.Voice
{
    /// <summary>
    /// Push-to-talk from a thumb-index pinch (either hand or the dominant one, per IUserPreferences); controllers are
    /// <see cref="ControllerPushToTalk"/>. A pinch starts talking only when that hand's ray hovers nothing (so pinching
    /// UI or poking Nubi does not talk) and the palm does not face the head (the Horizon OS system-menu gesture). The same
    /// gate picks the hand ray that aims point-and-tap (<see cref="TryGetAim"/>, ADR-0044).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PinchPushToTalk : MonoBehaviour, IVoiceActivation
    {
        // cos 53°: a palm turned toward the face, not one held sideways while pinching.
        const float PalmTowardHeadMinCos = 0.6f;

        [SerializeField] XRBaseInteractor leftHandRay;
        [SerializeField] XRBaseInteractor rightHandRay;
        [SerializeField] Handedness dominantHand = Handedness.Right;

        readonly GatedPinch _left = new();
        readonly GatedPinch _right = new();
        IHandSource _hands;
        IClock _clock;
        Transform _head;
        IUserPreferences _preferences;

        public bool IsHeld { get; private set; }
        public event Action Pressed;
        public event Action Released;

        public void Construct(IHandSource hands, IClock clock, Transform head, IUserPreferences preferences)
        {
            _hands = hands;
            _clock = clock;
            _head = head;
            _preferences = preferences;
        }

        void Update()
        {
            if (_hands == null)
                return;
            var eitherHand = _preferences == null || _preferences.Current.PttEitherHand;
            var held = Track(Handedness.Left, _left, leftHandRay, eitherHand)
                       | Track(Handedness.Right, _right, rightHandRay, eitherHand);
            SetHeld(held);
        }

        /// <summary>
        /// The ray of the hand a pinch comes from: the pinching hand while one is held, else the dominant hand, then the
        /// other when either hand may talk. Only a tracked hand whose ray is active and whose gate is open counts.
        /// </summary>
        public bool TryGetAim(out Ray ray)
        {
            ray = default;
            if (_hands == null)
                return false;
            if (_left.IsHeld || _right.IsHeld)
                return InteractorRays.TryGetRay(_left.IsHeld ? leftHandRay : rightHandRay, out ray);
            var other = dominantHand == Handedness.Left ? Handedness.Right : Handedness.Left;
            var eitherHand = _preferences == null || _preferences.Current.PttEitherHand;
            return TryFreeRay(dominantHand, out ray) || (eitherHand && TryFreeRay(other, out ray));
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
                return pinch.Update(null, gateOpen: false, _clock.Now);

            return pinch.Update(HandGestures.PinchDistance(hand), GateOpen(ray, hand), _clock.Now);
        }

        bool TryFreeRay(Handedness side, out Ray ray)
        {
            ray = default;
            var interactor = side == Handedness.Left ? leftHandRay : rightHandRay;
            return _hands.TryGetHand(side, out var hand) && GateOpen(interactor, hand) && InteractorRays.TryGetRay(interactor, out ray);
        }

        bool GateOpen(XRBaseInteractor ray, in HandSnapshot hand) =>
            !InteractorRays.IsBusy(ray) && !HandGestures.PalmFaces(hand, _head.position, PalmTowardHeadMinCos);
    }
}
