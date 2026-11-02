using Jolly.Contracts;

namespace Jolly.Character
{
    /// <summary>The Nubi.fbx take that plays each gesture. LookAround has no clip: it is a scripted look-at sweep.</summary>
    public static class GestureClips
    {
        public static bool TryGetClip(GestureId gesture, out string clip)
        {
            clip = gesture switch
            {
                GestureId.Wave => "Wave",
                GestureId.Nod => "Nod",
                GestureId.ShakeHead => "ShakeHead",
                GestureId.Shrug => "Shrug",
                GestureId.Point => "Point",
                GestureId.HappyBounce => "Happy",
                GestureId.Think => "Think",
                GestureId.Stretch => "Stretch",
                GestureId.Dance => "Dance",
                GestureId.Jump => "Jump",
                GestureId.Bow => "Bow",
                _ => null,
            };
            return clip != null;
        }

        /// <summary>
        /// Moves the hips and legs, so it plays on the unmasked Action layer: on the upper-body Gesture layer a jump
        /// would never leave the floor. These are also the optional clips: a character without them is still valid and
        /// simply does not offer the actions they back (ADR-0047).
        /// </summary>
        public static bool IsFullBody(GestureId gesture) =>
            gesture == GestureId.Dance || gesture == GestureId.Jump || gesture == GestureId.Bow;
    }
}
