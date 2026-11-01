using System.Collections.Generic;
using Jolly.Contracts;
using Jolly.XR.Hands;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Tracking
{
    /// <summary>
    /// User head (the XR Origin camera), pointing ray (a pointing hand, else the tracked controller's aim) and hand key
    /// points. Records the head pose every frame so camera frames can be matched to the pose at their exposure time
    /// (IHeadPoseHistory).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class XrUserPoseProvider : MonoBehaviour, IUserPoseProvider, IHeadPoseHistory
    {
        // ~1.3 s at 72 Hz: comfortably covers the 20–40 ms camera latency plus capture scheduling.
        const int HistoryFrames = 96;

        readonly PoseHistory _history = new(HistoryFrames);
        Transform _head;
        IHandSource _hands;
        Transform _controllerAim;
        double _headPoseLead;

        /// <param name="controllerAim">The controller ray origin; the rig deactivates it while the controller is not
        /// tracked. Null for hands only.</param>
        /// <param name="headPoseLeadSeconds">How far ahead of LateUpdate the OpenXR camera pose is predicted (display
        /// time), so history samples are stamped when the head was there. 0 until the R1 marker test measures it.</param>
        public void Construct(Transform head, IHandSource hands, Transform controllerAim = null, double headPoseLeadSeconds = 0)
        {
            _head = head;
            _hands = hands;
            _controllerAim = controllerAim;
            _headPoseLead = headPoseLeadSeconds;
        }

        public Pose Head => _head == null ? Pose.identity : new Pose(_head.position, _head.rotation);

        public bool TryGetPointingRay(out Ray ray)
        {
            if (_hands != null && (TryPointing(Handedness.Right, out ray) || TryPointing(Handedness.Left, out ray)))
                return true;
            if (_controllerAim != null && _controllerAim.gameObject.activeInHierarchy)
            {
                ray = new Ray(_controllerAim.position, _controllerAim.forward);
                return true;
            }
            ray = default;
            return false;
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
                _history.Add(Time.realtimeSinceStartupAsDouble + _headPoseLead, Head);
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
