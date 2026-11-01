using UnityEngine;

namespace Jolly.XR.Hands
{
    /// <summary>
    /// Pose-only hand classification (no Meta aim extension, so it ports to any OpenXR hand-tracking runtime).
    /// A finger counts as extended when its tip is clearly farther from the wrist than its knuckle; ratios are
    /// size-independent, so the same thresholds hold for small and large hands.
    /// </summary>
    public static class HandGestures
    {
        const float ExtendedRatio = 1.5f;
        const float CurledRatio = 1.25f;

        public static float PinchDistance(in HandSnapshot h) => Vector3.Distance(h.ThumbTip, h.IndexTip);

        public static bool IsPointing(in HandSnapshot h) =>
            IndexExtended(h) && Ratio(h, h.MiddleTip) < CurledRatio && Ratio(h, h.RingTip) < CurledRatio;

        public static bool IsOpen(in HandSnapshot h) =>
            IndexExtended(h) && Ratio(h, h.MiddleTip) > ExtendedRatio && Ratio(h, h.RingTip) > ExtendedRatio
            && Ratio(h, h.LittleTip) > ExtendedRatio * 0.9f;

        /// <summary>From the index knuckle through the index tip.</summary>
        public static Ray PointingRay(in HandSnapshot h) => new(h.IndexProximal, h.IndexTip - h.IndexProximal);

        /// <summary>True when the palm normal is within acos(minCos) of the direction to target.</summary>
        public static bool PalmFaces(in HandSnapshot h, Vector3 target, float minCos)
        {
            var toTarget = target - h.Palm;
            var normal = h.PalmRotation * Vector3.down;
            return toTarget.sqrMagnitude > 1e-8f && Vector3.Dot(normal, toTarget.normalized) >= minCos;
        }

        static bool IndexExtended(in HandSnapshot h)
        {
            var knuckle = Vector3.Distance(h.IndexProximal, h.Wrist);
            return knuckle > 1e-4f && Vector3.Distance(h.IndexTip, h.Wrist) > ExtendedRatio * knuckle;
        }

        static float Ratio(in HandSnapshot h, Vector3 tip)
        {
            var knuckle = Vector3.Distance(h.MiddleProximal, h.Wrist);
            return knuckle > 1e-4f ? Vector3.Distance(tip, h.Wrist) / knuckle : 0f;
        }
    }
}
