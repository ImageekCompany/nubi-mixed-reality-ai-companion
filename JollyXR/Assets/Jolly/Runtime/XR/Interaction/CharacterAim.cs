using UnityEngine;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// Whether a ray through the character's collider means the character or the floor behind it (ADR-0044). It means the
    /// floor only when it enters the collider below the knee (<see cref="KneeMeters"/>) and lands within
    /// <see cref="JustPastMeters"/> beyond the footprint. The landing alone was not enough: at normal standing distance, a
    /// hand at 1.1 m aimed at the chest of a character 1 m away lands 0.6 m past, so the ring showed, a tap walked and a
    /// hold waited the tap time on a pinch aimed at the character. So near, the floor behind the character takes aiming
    /// beside it.
    /// </summary>
    public static class CharacterAim
    {
        public const float JustPastMeters = 0.5f;
        public const float KneeMeters = 0.25f;

        /// <summary>False for a ray that misses <paramref name="character"/> (or a disabled one) as for one meaning the floor.</summary>
        public static bool AimsAt(Collider character, Ray ray, float maxDistance)
        {
            if (character == null || !character.enabled || !character.Raycast(ray, out var hit, maxDistance))
                return false;
            var bounds = character.bounds;
            var feet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return hit.point.y - feet.y > KneeMeters || !LandsJustPast(ray, feet, bounds.extents.x);
        }

        /// <param name="feet">The middle of the character's footprint.</param>
        /// <param name="radius">The footprint's radius; a ray landing inside it is on the character.</param>
        public static bool LandsJustPast(Ray ray, Vector3 feet, float radius)
        {
            if (ray.direction.y > -1e-3f)
                return false;
            var distance = (feet.y - ray.origin.y) / ray.direction.y;
            if (distance <= 0f)
                return false;
            var landing = ray.GetPoint(distance);
            var past = new Vector2(landing.x - feet.x, landing.z - feet.z).magnitude;
            return past > radius && past <= radius + JustPastMeters;
        }
    }
}
