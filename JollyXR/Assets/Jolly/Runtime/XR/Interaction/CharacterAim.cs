using UnityEngine;

namespace Jolly.XR.Interaction
{
    /// <summary>
    /// Whether a ray through the character's collider means the floor behind it (ADR-0044). From a hand, floor just behind
    /// the character is reached through its legs, and treating every such ray as "at the character" showed no ring
    /// there. A ray that lands within <see cref="JustPastMeters"/> beyond its footprint came in low enough to mean that
    /// floor. Aimed at the chest from a hand at 1.1 m, 1.5 m away, the ray lands about 1 m past, so it stays a pinch at the
    /// character. Pure.
    /// </summary>
    public static class CharacterAim
    {
        public const float JustPastMeters = 0.5f;

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
