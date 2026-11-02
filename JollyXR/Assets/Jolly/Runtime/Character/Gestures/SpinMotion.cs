using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// A turn on the spot of a set number of degrees over <see cref="SecondsPerTurn"/> per full turn, eased in and out
    /// (smoothstep), so a whole number of turns ends exactly where it began. Plain C#: the controller applies each step's
    /// yaw to its root.
    /// </summary>
    public sealed class SpinMotion
    {
        public const float SecondsPerTurn = 1.2f;
        /// <summary>A short spin still reads as a deliberate move, not a twitch.</summary>
        const float MinSeconds = 0.5f;

        readonly float _degrees;
        float _elapsed;
        float _turned;

        public SpinMotion(float degrees)
        {
            _degrees = degrees;
            Duration = Mathf.Max(MinSeconds, Mathf.Abs(degrees) / 360f * SecondsPerTurn);
        }

        public float Duration { get; }
        public bool Done => _elapsed >= Duration;

        /// <summary>Signed degrees to turn this frame; their sum over the motion is exactly the requested angle.</summary>
        public float Step(float deltaTime)
        {
            if (Done)
                return 0f;
            _elapsed = Mathf.Min(_elapsed + deltaTime, Duration);
            var target = _degrees * Mathf.SmoothStep(0f, 1f, _elapsed / Duration);
            var step = target - _turned;
            _turned = target;
            return step;
        }
    }
}
