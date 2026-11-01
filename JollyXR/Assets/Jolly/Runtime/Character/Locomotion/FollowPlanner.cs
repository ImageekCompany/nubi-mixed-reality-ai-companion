using UnityEngine;

namespace Jolly.Character
{
    /// <summary>Where to stand while following: on the line to the user, in the middle of the comfort band.</summary>
    public static class FollowPlanner
    {
        /// <summary>False while Nubi is already inside [min, max] of the user (horizontal distance): stay put.</summary>
        public static bool TryGetGoal(Vector3 self, Vector3 user, float distanceMin, float distanceMax, out Vector3 goal)
        {
            var offset = self - user;
            offset.y = 0f;
            var distance = offset.magnitude;
            if (distance >= distanceMin && distance <= distanceMax)
            {
                goal = self;
                return false;
            }

            var away = distance > 1e-3f ? offset / distance : Vector3.back;
            goal = new Vector3(user.x, self.y, user.z) + away * (0.5f * (distanceMin + distanceMax));
            return true;
        }
    }
}
