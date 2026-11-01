using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// Holds a small control just off the inside of a wrist while that palm faces the head, and hides it otherwise
    /// (ADR-0042): the settings button is there when the user looks at their wrist and gone the rest of the time. The view
    /// on the same object draws and hides itself on <see cref="ShownChanged"/>. The show and hide angles differ so it does
    /// not flicker at the edge, and a short loss of tracking (the other hand covering this one to press the button) keeps
    /// it where it was.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WristAnchor : MonoBehaviour
    {
        // cos 50° to appear, cos 65° to go.
        const float ShowMinCos = 0.64f;
        const float KeepMinCos = 0.42f;

        [SerializeField] Handedness _hand = Handedness.Left;
        [Tooltip("Hand space (+Z along the fingers, -Y out of the palm), from the wrist joint: toward the forearm and off the palm.")]
        [SerializeField] Vector3 _wristOffset = new(0f, -0.035f, -0.025f);
        [Tooltip("Seconds the control stays put after the hand stops being tracked.")]
        [SerializeField] float _lostGraceSeconds = 0.4f;

        IHandSource _hands;
        Transform _head;
        IClock _clock;
        double _lostAt = double.NaN;

        public bool IsShown { get; private set; }
        public event Action<bool> ShownChanged;

        public void Construct(IHandSource hands, Transform head, IClock clock)
        {
            _hands = hands;
            _head = head;
            _clock = clock;
        }

        void LateUpdate()
        {
            if (_hands == null)
                return;
            if (!_hands.TryGetHand(_hand, out var hand))
            {
                if (double.IsNaN(_lostAt))
                    _lostAt = _clock.Now;
                if (_clock.Now - _lostAt >= _lostGraceSeconds)
                    SetShown(false);
                return;
            }
            _lostAt = double.NaN;
            var head = _head.position;
            SetShown(HandGestures.PalmFaces(hand, head, IsShown ? KeepMinCos : ShowMinCos));
            if (!IsShown)
                return;
            var position = hand.Wrist + hand.PalmRotation * _wristOffset;
            // A world-space canvas reads correctly with its forward pointing away from the viewer.
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - head, Vector3.up));
        }

        void OnDisable() => SetShown(false);

        void SetShown(bool shown)
        {
            if (shown == IsShown)
                return;
            IsShown = shown;
            ShownChanged?.Invoke(shown);
        }
    }
}
