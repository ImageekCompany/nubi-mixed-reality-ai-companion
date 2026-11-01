using Jolly.XR.Hands;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// An open hand held with the palm toward Nubi for a short moment (a greeting / "stop" gesture). Geometric check on
    /// the same joints as push-to-talk instead of the XR Hands Gestures sample's hand-shape assets.
    /// </summary>
    public sealed class OpenPalmDetector
    {
        const double HoldSeconds = 0.4;
        // cos 45°.
        const float TowardTargetMinCos = 0.7f;

        readonly DwellTimer _timer = new(HoldSeconds);

        public bool Update(IHandSource hands, Vector3 target, double now) =>
            _timer.Update(IsOffered(hands, Handedness.Left, target) || IsOffered(hands, Handedness.Right, target), now);

        static bool IsOffered(IHandSource hands, Handedness side, Vector3 target) =>
            hands.TryGetHand(side, out var hand) && HandGestures.IsOpen(hand) && HandGestures.PalmFaces(hand, target, TowardTargetMinCos);
    }
}
