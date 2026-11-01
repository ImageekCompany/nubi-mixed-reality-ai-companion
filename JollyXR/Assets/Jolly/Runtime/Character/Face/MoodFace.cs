using Jolly.Contracts;

namespace Jolly.Character
{
    /// <summary>Resting expression per mood, as face weights (0..1). Clip presets and lip-sync layer over it.</summary>
    public static class MoodFace
    {
        static readonly float[][] Weights = Build();

        /// <summary>Indexed by <see cref="FaceChannel"/>. Do not mutate.</summary>
        public static float[] For(Mood mood)
        {
            var i = (int)mood;
            return i >= 0 && i < Weights.Length ? Weights[i] : Weights[(int)Mood.Neutral];
        }

        static float[][] Build()
        {
            var table = new float[(int)Mood.Sleepy + 1][];
            for (var i = 0; i < table.Length; i++)
                table[i] = new float[FaceChannels.Count];

            table[(int)Mood.Happy][(int)FaceChannel.Smile] = 0.5f;
            table[(int)Mood.Curious][(int)FaceChannel.WideEyes] = 0.35f;
            table[(int)Mood.Curious][(int)FaceChannel.Smile] = 0.15f;
            table[(int)Mood.Concerned][(int)FaceChannel.Frown] = 0.6f;
            table[(int)Mood.Concerned][(int)FaceChannel.SadEyes] = 0.6f;
            table[(int)Mood.Sleepy][(int)FaceChannel.BlinkLeft] = 0.45f;
            table[(int)Mood.Sleepy][(int)FaceChannel.BlinkRight] = 0.45f;
            return table;
        }
    }
}
