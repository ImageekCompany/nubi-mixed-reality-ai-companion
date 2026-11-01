using UnityEngine.XR.Hands;

namespace Jolly.XR.Hands
{
    /// <summary>Per-frame hand joints in world space. The seam that lets PlayMode tests drive gestures without a runtime.</summary>
    public interface IHandSource
    {
        bool TryGetHand(Handedness handedness, out HandSnapshot snapshot);
    }
}
