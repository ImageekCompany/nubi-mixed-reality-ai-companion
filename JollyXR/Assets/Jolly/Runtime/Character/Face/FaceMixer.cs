using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Layers the face: the expression (the playing clip's preset, or the mood where stronger) cross-fades in;
    /// lip-sync vowels replace the expression's mouth in proportion to how open the mouth is; blinks go on top
    /// unless the eyes are already closed arcs (Happy_Eyes). Output weights are 0..1 per <see cref="FaceChannel"/>.
    /// </summary>
    public sealed class FaceMixer
    {
        readonly FaceSettings _settings;
        readonly float[] _expression = new float[FaceChannels.Count];
        readonly float[] _output = new float[FaceChannels.Count];

        public FaceMixer(FaceSettings settings) => _settings = settings;

        /// <summary>Indexed by <see cref="FaceChannel"/>. Valid until the next <see cref="Mix"/>.</summary>
        public float[] Output => _output;

        public void Mix(float[] clipPreset, float[] mood, MouthSmoother mouth, float blink, float deltaTime)
        {
            var step = deltaTime / Mathf.Max(_settings.presetFadeSeconds, 1e-3f);
            for (var c = 0; c < FaceChannels.Count; c++)
            {
                var target = Mathf.Max(clipPreset != null ? clipPreset[c] : 0f, mood[c]);
                _expression[c] = Mathf.MoveTowards(_expression[c], target, step);
                _output[c] = _expression[c];
            }

            var keep = 1f - mouth.Openness;
            Vowel(FaceChannel.A, mouth.A, keep);
            Vowel(FaceChannel.I, mouth.I, keep);
            Vowel(FaceChannel.U, mouth.U, keep);
            Vowel(FaceChannel.E, mouth.E, keep);
            Vowel(FaceChannel.O, mouth.O, keep);

            var lids = _expression[(int)FaceChannel.HappyEyes] > _settings.happyEyesBlinkSuppress ? 0f : blink;
            Blink(FaceChannel.BlinkLeft, lids);
            Blink(FaceChannel.BlinkRight, lids);
        }

        void Vowel(FaceChannel c, float lip, float keep) =>
            _output[(int)c] = Mathf.Clamp01(lip + _expression[(int)c] * keep);

        void Blink(FaceChannel c, float lids) =>
            _output[(int)c] = Mathf.Max(_expression[(int)c], lids);
    }
}
