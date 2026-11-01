using UnityEngine;

namespace Jolly.Character
{
    public readonly struct ContactShadowShape
    {
        public ContactShadowShape(float opacity, float scale)
        {
            Opacity = opacity;
            Scale = scale;
        }

        /// <summary>Multiplier on the material's standing opacity, 0..1.</summary>
        public float Opacity { get; }
        /// <summary>Multiplier on the quad's standing size.</summary>
        public float Scale { get; }
    }

    /// <summary>How Nubi's contact shadow reacts to its feet leaving the floor (hops, HappyBounce).</summary>
    public static class ContactShadowMath
    {
        /// <summary>Height of the lower foot above where it rests on the floor, never negative. Walking keeps one foot
        /// planted, so only both-feet airtime lifts the shadow.</summary>
        public static float Lift(float leftFootY, float rightFootY, float floorY, float footRestHeight) =>
            Mathf.Max(0f, Mathf.Min(leftFootY, rightFootY) - floorY - footRestHeight);

        public static ContactShadowShape Shape(float lift, ContactShadowSettings settings)
        {
            var t = settings.fadeHeight > 0f ? Mathf.Clamp01(lift / settings.fadeHeight) : 0f;
            return new ContactShadowShape(1f - t, Mathf.Lerp(1f, settings.scaleAtFadeHeight, t));
        }
    }
}
