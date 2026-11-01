namespace Jolly.Character
{
    /// <summary>
    /// Canonical face channels the face layers drive. Each character's definition maps them onto its own blendshapes
    /// or jaw (<see cref="FaceChannelBinding"/>); Nubi's shape keys carry exactly these names.
    /// </summary>
    public enum FaceChannel
    {
        Silence, A, I, U, E, O,
        Smile, Frown,
        BlinkLeft, BlinkRight, HappyEyes, WideEyes, SadEyes,
    }

    public static class FaceChannels
    {
        public const int Count = 13;

        /// <summary>Canonical channel names, indexed by <see cref="FaceChannel"/>: face presets and definitions use them.</summary>
        public static readonly string[] ShapeNames =
        {
            "sil", "A", "I", "U", "E", "O",
            "Smile", "Frown",
            "Blink_L", "Blink_R", "Happy_Eyes", "Wide_Eyes", "Sad_Eyes",
        };

        public static bool TryParse(string shapeName, out FaceChannel channel)
        {
            for (var i = 0; i < ShapeNames.Length; i++)
            {
                if (ShapeNames[i] == shapeName)
                {
                    channel = (FaceChannel)i;
                    return true;
                }
            }
            channel = default;
            return false;
        }
    }
}
