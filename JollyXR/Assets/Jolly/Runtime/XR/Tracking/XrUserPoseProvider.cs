using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.XR.Hands;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Tracking
{
    /// <summary>
    /// User head (the XR Origin camera), hand pointing ray and hand key points. Records the head pose every frame
    /// so camera frames can be matched to the pose at their exposure time (IHeadPoseHistory).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XrUserPoseProvider : MonoBehaviour, IUserPoseProvider, IHeadPoseHistory
    {
        // ~1.3 s at 72 Hz: comfortably covers the 20–40 ms camera latency plus capture scheduling.
        const int HistoryFrames = 96;

        readonly PoseHistory _history = new(HistoryFrames);
        Transform _head;
        IHandSource _hands;

        public void Construct(Transform head, IHandSource hands)
        {
            _head = head;
            _hands = hands;
        }

        public Pose Head => _head == null ? Pose.identity : new Pose(_head.position, _head.rotation);

        public bool TryGetPointingRay(out Ray ray)
        {
            ray = default;
            if (_hands == null)
                return false;
            return TryPointing(Handedness.Right, out ray) || TryPointing(Handedness.Left, out ray);
        }

        public void GetHandKeyPoints(List<Vector3> into)
        {
            into.Clear();
            if (_hands == null)
                return;
            AddKeyPoints(Handedness.Left, into);
            AddKeyPoints(Handedness.Right, into);
        }

        public bool TryGetHeadAt(double realtimeSinceStartup, out Pose head) => _history.TrySample(realtimeSinceStartup, out head);

        void LateUpdate()
        {
            if (_head != null)
                _history.Add(Time.realtimeSinceStartupAsDouble, Head);
        }

        bool TryPointing(Handedness side, out Ray ray)
        {
            ray = default;
            if (!_hands.TryGetHand(side, out var hand) || !HandGestures.IsPointing(hand))
                return false;
            ray = HandGestures.PointingRay(hand);
            return true;
        }

        void AddKeyPoints(Handedness side, List<Vector3> into)
        {
            if (!_hands.TryGetHand(side, out var hand))
                return;
            into.Add(hand.Wrist);
            into.Add(hand.Palm);
            into.Add(hand.ThumbTip);
            into.Add(hand.IndexTip);
            into.Add(hand.MiddleTip);
        }
    }
}
