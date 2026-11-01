using UnityEngine;

namespace Jolly.XR.Hands
{
    /// <summary>The joints Nubi's gestures need, in world space. Palm rotation follows XR Hands: -Y out of the palm,
    /// +Z along the fingers.</summary>
    public readonly struct HandSnapshot
    {
        public HandSnapshot(Vector3 wrist, Vector3 palm, Quaternion palmRotation, Vector3 thumbTip, Vector3 indexProximal,
                            Vector3 indexTip, Vector3 middleProximal, Vector3 middleTip, Vector3 ringTip, Vector3 littleTip)
        {
            Wrist = wrist; Palm = palm; PalmRotation = palmRotation; ThumbTip = thumbTip; IndexProximal = indexProximal;
            IndexTip = indexTip; MiddleProximal = middleProximal; MiddleTip = middleTip; RingTip = ringTip; LittleTip = littleTip;
        }

        public Vector3 Wrist { get; }
        public Vector3 Palm { get; }
        public Quaternion PalmRotation { get; }
        public Vector3 ThumbTip { get; }
        public Vector3 IndexProximal { get; }
        public Vector3 IndexTip { get; }
        public Vector3 MiddleProximal { get; }
        public Vector3 MiddleTip { get; }
        public Vector3 RingTip { get; }
        public Vector3 LittleTip { get; }
    }
}
