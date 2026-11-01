using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    public enum AttentionTarget { None, User, Point }

    /// <summary>
    /// Where the character looks this frame (ADR-0041). A tool's point holds only for
    /// <see cref="LookSettings.pointHoldSeconds"/> of standing still; otherwise the character attends the user whenever a
    /// conversation turn is on, it follows, or the user is within <see cref="LookSettings.attendWithin"/>. Attending the
    /// user, the body faces them past <see cref="LookSettings.faceUserBeyond"/>.
    /// </summary>
    public static class Attention
    {
        /// <summary>The in-place turn slows over its last degrees: deg/s per degree still to go.</summary>
        const float TurnEasePerSecond = 4f;
        /// <summary>Never slower than this, so the last degrees do not crawl, deg/s.</summary>
        const float MinTurnSpeed = 30f;

        public static AttentionTarget Resolve(LookTargetKind requested, bool pointHeld, bool inConversation, bool following,
                                              float userDistance, LookSettings settings)
        {
            if (requested == LookTargetKind.WorldPoint && pointHeld)
                return AttentionTarget.Point;
            if (requested == LookTargetKind.User || inConversation || following || userDistance <= settings.attendWithin)
                return AttentionTarget.User;
            return AttentionTarget.None;
        }

        /// <summary>Signed degrees to turn the body this frame toward <paramref name="yaw"/>, never past it.</summary>
        public static float BodyTurnStep(float yaw, float maxSpeed, float deltaTime)
        {
            var off = Mathf.Abs(yaw);
            var speed = Mathf.Clamp(off * TurnEasePerSecond, MinTurnSpeed, Mathf.Max(maxSpeed, MinTurnSpeed));
            return Mathf.Sign(yaw) * Mathf.Min(off, speed * deltaTime);
        }
    }
}
