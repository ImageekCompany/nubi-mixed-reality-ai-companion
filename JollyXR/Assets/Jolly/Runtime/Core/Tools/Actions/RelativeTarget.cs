using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    public enum RelativeDirection { Front, Behind, Left, Right, Closer, Away }

    /// <summary>
    /// Where "go behind me" and its kin put the character (ADR-0047): a floor point at the character's height, from the
    /// user's head yaw only, so looking down or up changes nothing. Front, behind, left and right are
    /// <c>distance</c> from the user in that direction of their body; closer is <see cref="LocomotionSettings.CloserDistance"/>
    /// from the user towards the character; away backs the character off by <c>distance</c> along the user → character line.
    /// </summary>
    public static class RelativeTarget
    {
        public static Vector3 Resolve(RelativeDirection direction, float distance, Pose head, Vector3 character,
                                      LocomotionSettings settings)
        {
            var user = new Vector3(head.position.x, character.y, head.position.z);
            var forward = Flat(head.rotation * Vector3.forward, Vector3.forward);
            var right = Vector3.Cross(Vector3.up, forward);
            // Standing on the user's spot the line is undefined; in front of them is where they would see the move.
            var outward = Flat(character - user, forward);
            return direction switch
            {
                RelativeDirection.Front => user + forward * distance,
                RelativeDirection.Behind => user - forward * distance,
                RelativeDirection.Left => user - right * distance,
                RelativeDirection.Right => user + right * distance,
                RelativeDirection.Closer => user + outward * settings.CloserDistance,
                _ => character + outward * distance,
            };
        }

        /// <summary>Horizontal distance from <paramref name="user"/> to the nearest point of the segment from → to, m.</summary>
        public static float PassDistance(Vector3 from, Vector3 to, Vector3 user)
        {
            var a = new Vector2(from.x, from.z);
            var ab = new Vector2(to.x, to.z) - a;
            var au = new Vector2(user.x, user.z) - a;
            var t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(au, ab) / ab.sqrMagnitude);
            return (au - ab * t).magnitude;
        }

        /// <summary>
        /// Where a walk from → to turns to go round the user: <paramref name="radius"/> from them, square to the walk, on
        /// the side of the user the character starts on (the walk's right when it starts dead ahead of them), or on the
        /// other side. At the character's height.
        /// </summary>
        public static Vector3 Detour(Vector3 from, Vector3 to, Vector3 user, float radius, bool otherSide)
        {
            var travel = Flat(to - from, Vector3.forward);
            var side = Vector3.Cross(Vector3.up, travel);
            var offset = from - user;
            offset.y = 0f;
            if (Vector3.Dot(offset, side) < -1e-3f)
                side = -side;
            if (otherSide)
                side = -side;
            return new Vector3(user.x, from.y, user.z) + side * radius;
        }

        /// <summary>Horizontal distance between two points, m.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? fallback : v.normalized;
        }
    }
}
