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
                _ => null,
            };
            return clip != null;
        }
    }
}
