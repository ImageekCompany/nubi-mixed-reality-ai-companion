using UnityEngine.InputSystem;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// Touch controller buttons bound by layout name, never by usage (ADR-0021): a hand can surface as a controller (the
    /// Hand Interaction profile, a runtime that reports a pinch as a trigger, the KHR simple controller), and a press
    /// from one of those must not skip the pinch gating.
    /// </summary>
    static class TouchBindings
    {
        static readonly string[] Layouts = { "OculusTouchController", "QuestTouchPlusController", "QuestProTouchController" };

        /// <param name="hand">"{LeftHand}" or "{RightHand}".</param>
        /// <param name="control">The control on the Touch layouts, such as "primaryButton" or "triggerPressed".</param>
        public static InputAction Button(string name, string hand, string control)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var layout in Layouts)
                action.AddBinding("<" + layout + ">" + hand + "/" + control);
            return action;
        }
    }
}
