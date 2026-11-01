using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using Jolly.XR.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// <see cref="IPointerInput"/> over the rig (ADR-0044): the hand ray <see cref="PinchPushToTalk"/> would pinch with,
    /// else the far ray of a Touch controller, the side whose trigger was pressed last first. A controller side whose
    /// hand is tracked is in hand mode and ignored, as in <see cref="ControllerPushToTalk"/>. A ray that hovers UI or an
    /// interactable, or hits the character's collider, is not free. The trigger is bound by Touch layout name
    /// (<see cref="TouchBindings"/>); XRI still uses it for UI presses and Activate, which is why a busy ray is skipped.
    /// </summary>
    public sealed class XrPointer : IPointerInput, IDisposable
    {
        const float CharacterHitDistance = 10f;

        readonly PinchPushToTalk _pinch;
        readonly IHandSource _hands;
        readonly XRBaseInteractor _leftController;
        readonly XRBaseInteractor _rightController;
        readonly Collider _character;
        readonly InputAction _leftTrigger;
        readonly InputAction _rightTrigger;
        Handedness _lastController = Handedness.Right;

        /// <param name="modality">The rig's modality manager; its controller objects hold the Near-Far interactors.</param>
        /// <param name="character">Nubi's collider; null when there is none to avoid.</param>
        public XrPointer(PinchPushToTalk pinch, IHandSource hands, XRInputModalityManager modality, Collider character)
        {
            _pinch = pinch;
            _hands = hands;
            _character = character;
            _leftController = FarInteractor(modality != null ? modality.leftController : null);
            _rightController = FarInteractor(modality != null ? modality.rightController : null);
            _leftTrigger = TouchBindings.Button("Move Left", "{LeftHand}", "triggerPressed");
            _rightTrigger = TouchBindings.Button("Move Right", "{RightHand}", "triggerPressed");
            _leftTrigger.performed += OnLeftTrigger;
            _rightTrigger.performed += OnRightTrigger;
            _leftTrigger.Enable();
            _rightTrigger.Enable();
        }

        public event Action<Ray> TriggerPressed;

        public bool TryGetAim(out Ray ray, out PointerSource source)
        {
            source = PointerSource.Hand;
            if (_pinch != null && _pinch.TryGetAim(out ray) && !HitsCharacter(ray))
                return true;
            source = PointerSource.Controller;
            return TryController(_lastController, out ray) || TryController(Other(_lastController), out ray);
        }

        public void Dispose()
        {
            _leftTrigger.performed -= OnLeftTrigger;
            _rightTrigger.performed -= OnRightTrigger;
            _leftTrigger.Dispose();
            _rightTrigger.Dispose();
        }

        void OnLeftTrigger(InputAction.CallbackContext _) => OnTrigger(Handedness.Left);
        void OnRightTrigger(InputAction.CallbackContext _) => OnTrigger(Handedness.Right);

        void OnTrigger(Handedness side)
        {
            if (_hands != null && _hands.TryGetHand(side, out _))
                return;
            _lastController = side;
            if (TryController(side, out var ray))
                TriggerPressed?.Invoke(ray);
        }

        bool TryController(Handedness side, out Ray ray)
        {
            ray = default;
            var interactor = side == Handedness.Left ? _leftController : _rightController;
            if (_hands != null && _hands.TryGetHand(side, out _))
                return false;
            return !InteractorRays.IsBusy(interactor) && InteractorRays.TryGetRay(interactor, out ray) && !HitsCharacter(ray);
        }

        bool HitsCharacter(Ray ray) => _character != null && _character.enabled && _character.Raycast(ray, out _, CharacterHitDistance);

        static Handedness Other(Handedness side) => side == Handedness.Left ? Handedness.Right : Handedness.Left;

        static XRBaseInteractor FarInteractor(GameObject controller) =>
            controller != null ? controller.GetComponentInChildren<NearFarInteractor>(true) : null;
    }
}
