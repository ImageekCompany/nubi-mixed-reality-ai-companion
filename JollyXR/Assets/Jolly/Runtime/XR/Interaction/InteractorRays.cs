using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Jolly.XR.Interaction
{
    /// <summary>What an XRI ray interactor is pointing at, for the pinch gate and point-and-tap (ADR-0030, ADR-0044).</summary>
    public static class InteractorRays
    {
        /// <summary>True while the ray hovers an interactable or a UI element: a press there belongs to XRI.</summary>
        public static bool IsBusy(XRBaseInteractor interactor)
        {
            if (interactor == null)
                return false;
            if (interactor.hasHover)
                return true;
            return interactor is NearFarInteractor nearFar && nearFar.TryGetUIModel(out var model) && model.currentRaycast.isValid;
        }

        /// <summary>The straight far ray of an active Near-Far interactor from its stabilised cast origin (the XRI
        /// default casts one curve segment, a straight line). False when the interactor is inactive, as XRI's modality
        /// manager makes the side not in use.</summary>
        public static bool TryGetRay(XRBaseInteractor interactor, out Ray ray)
        {
            ray = default;
            if (interactor is not NearFarInteractor nearFar || !nearFar.isActiveAndEnabled || nearFar.farInteractionCaster == null)
                return false;
            var origin = nearFar.curveOrigin;
            if (origin == null)
                return false;
            ray = new Ray(origin.position, origin.forward);
            return true;
        }
    }
}
