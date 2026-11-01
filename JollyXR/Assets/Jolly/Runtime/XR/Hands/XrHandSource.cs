using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Hands
{
    /// <summary>
    /// IHandSource over the XR Hands subsystem. Joint poses are relative to the XR Origin, so they are transformed by
    /// the origin's transform (as XR Hands' own gesture utilities do). Allocation-free once the subsystem is found.
    /// </summary>
    public sealed class XrHandSource : IHandSource
    {
        static readonly List<XRHandSubsystem> Subsystems = new();

        readonly Transform _origin;
        XRHandSubsystem _subsystem;

        public XrHandSource(Transform xrOrigin) => _origin = xrOrigin;

        public bool TryGetHand(Handedness handedness, out HandSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetSubsystem(out var subsystem))
                return false;
            var hand = handedness == Handedness.Left ? subsystem.leftHand : subsystem.rightHand;
            if (!hand.isTracked
                || !TryGet(hand, XRHandJointID.Wrist, out var wrist)
                || !TryGet(hand, XRHandJointID.Palm, out var palm)
                || !TryGet(hand, XRHandJointID.ThumbTip, out var thumbTip)
                || !TryGet(hand, XRHandJointID.IndexProximal, out var indexProximal)
                || !TryGet(hand, XRHandJointID.IndexTip, out var indexTip)
                || !TryGet(hand, XRHandJointID.MiddleProximal, out var middleProximal)
                || !TryGet(hand, XRHandJointID.MiddleTip, out var middleTip)
                || !TryGet(hand, XRHandJointID.RingTip, out var ringTip)
                || !TryGet(hand, XRHandJointID.LittleTip, out var littleTip))
                return false;

            snapshot = new HandSnapshot(wrist.position, palm.position, palm.rotation, thumbTip.position, indexProximal.position,
                indexTip.position, middleProximal.position, middleTip.position, ringTip.position, littleTip.position);
            return true;
        }

        bool TryGet(in XRHand hand, XRHandJointID id, out Pose world)
        {
            if (!hand.GetJoint(id).TryGetPose(out var local))
            {
                world = default;
                return false;
            }
            world = new Pose(_origin.TransformPoint(local.position), _origin.rotation * local.rotation);
            return true;
        }

        bool TryGetSubsystem(out XRHandSubsystem subsystem)
        {
            if (_subsystem == null || !_subsystem.running)
            {
                _subsystem = null;
                SubsystemManager.GetSubsystems(Subsystems);
                foreach (var candidate in Subsystems)
                {
                    if (candidate.running)
                    {
                        _subsystem = candidate;
                        break;
                    }
                }
                Subsystems.Clear();
            }
            subsystem = _subsystem;
            return subsystem != null;
        }
    }
}
