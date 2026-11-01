using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Jolly.XR.Interaction
{
    /// <summary>Raw gaze-dwell, open-palm and poke detections on Nubi (IInteractionSignals). Rate limiting is WP10's
    /// InteractionTriggers.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionSignals : MonoBehaviour, IInteractionSignals
    {
        readonly GazeDwellDetector _gaze = new();
        readonly OpenPalmDetector _palm = new();
        IUserPoseProvider _user;
        IHandSource _hands;
        IClock _clock;
        Collider _nubiCollider;
        XRBaseInteractable _nubiInteractable;

        public event Action GazeDwell;
        public event Action OpenPalm;
        public event Action Poke;

        /// <param name="nubiInteractable">Nubi's XRI interactable; only selections by an XRPokeInteractor count as a poke.</param>
        public void Construct(IUserPoseProvider user, IHandSource hands, IClock clock, Collider nubiCollider,
                              XRBaseInteractable nubiInteractable)
        {
            Unsubscribe();
            _user = user;
            _hands = hands;
            _clock = clock;
            _nubiCollider = nubiCollider;
            _nubiInteractable = nubiInteractable;
            if (isActiveAndEnabled)
                Subscribe();
        }

        void OnEnable() => Subscribe();
        void OnDisable() => Unsubscribe();

        void Update()
        {
            if (_clock == null || _nubiCollider == null)
                return;
            var bounds = _nubiCollider.bounds;
            var now = _clock.Now;
            if (_gaze.Update(_user.Head, bounds, now))
                GazeDwell?.Invoke();
            if (_palm.Update(_hands, bounds.center, now))
                OpenPalm?.Invoke();
        }

        void Subscribe()
        {
            if (_nubiInteractable != null)
                _nubiInteractable.selectEntered.AddListener(OnSelectEntered);
        }

        void Unsubscribe()
        {
            if (_nubiInteractable != null)
                _nubiInteractable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRPokeInteractor)
                Poke?.Invoke();
        }
    }
}
