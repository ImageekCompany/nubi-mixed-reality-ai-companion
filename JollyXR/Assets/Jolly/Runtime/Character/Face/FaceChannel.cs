namespace Jolly.Character
{
    /// <summary>Nubi_Mesh blendshapes the face layers drive (docs/art/nubi/README.md, Specs).</summary>
    public enum FaceChannel
    {
        Silence, A, I, U, E, O,
        Smile, Frown,
        BlinkLeft, BlinkRight, HappyEyes, WideEyes, SadEyes,
    }

    public static class FaceChannels
    {
        public const int Count = 13;

        /// <summary>Blendshape names, indexed by <see cref="FaceChannel"/>.</summary>
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
