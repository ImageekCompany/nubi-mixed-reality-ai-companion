using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// Smooths lip-sync vowel weights with a fast attack and a slower release (architecture §5.5: 60/90 ms),
    /// so visemes do not flicker frame to frame yet the mouth still closes promptly in pauses.
    /// </summary>
    public sealed class MouthSmoother
    {
        readonly float[] _vowels = new float[5];
        readonly FaceSettings _settings;

        public MouthSmoother(FaceSettings settings) => _settings = settings;

        public float A => _vowels[0];
        public float I => _vowels[1];
        public float U => _vowels[2];
        public float E => _vowels[3];
        public float O => _vowels[4];

        /// <summary>How open the mouth is overall, 0..1.</summary>
        public float Openness => Mathf.Clamp01(A + I + U + E + O);

        public void Step(MouthShape target, float deltaTime)
        {
            Approach(0, target.A, deltaTime);
            Approach(1, target.I, deltaTime);
            Approach(2, target.U, deltaTime);
            Approach(3, target.E, deltaTime);
            Approach(4, target.O, deltaTime);
        }

        public void Reset() => System.Array.Clear(_vowels, 0, _vowels.Length);

        void Approach(int i, float target, float deltaTime)
        {
            target = Mathf.Clamp01(target);
            var tau = target > _vowels[i] ? _settings.mouthAttackSeconds : _settings.mouthReleaseSeconds;
            _vowels[i] += (target - _vowels[i]) * (1f - Mathf.Exp(-deltaTime / Mathf.Max(tau, 1e-4f)));
        }
    }
}
