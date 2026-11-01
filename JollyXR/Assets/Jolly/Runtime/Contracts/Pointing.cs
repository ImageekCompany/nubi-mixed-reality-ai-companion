using System;
using UnityEngine;

namespace Jolly.Contracts
{
    public enum PointerSource { Hand, Controller }

    /// <summary>
    /// The rays the user sends the character with (ADR-0044): a hand that may pinch, else a Touch controller. A ray is
    /// free only when it hovers no UI or interactable, misses the character, and (a hand) the palm does not face the head.
    /// Hands tap through the pinch push-to-talk; controllers through the trigger. Main thread.
    /// </summary>
    public interface IPointerInput
    {
        /// <summary>The ray to aim with this frame: the pinching hand while a pinch is held, else the free hand or
        /// controller ray of the side used last. False when none is free.</summary>
        bool TryGetAim(out Ray ray, out PointerSource source);
        /// <summary>A controller trigger press whose ray was free, with that ray. Never raised for a hand.</summary>
        event Action<Ray> TriggerPressed;
    }

    /// <summary>The floor ring that shows where a tap sends the character.</summary>
    public interface IMoveMarker
    {
        void Show(Vector3 floorPoint);
        void Hide();
        /// <summary>A move was sent there: a short pulse, then hidden.</summary>
        void Confirm(Vector3 floorPoint);
    }
}
