using UnityEngine;

namespace Jolly.Character
{
    /// <summary>
    /// How far the fur tips lag behind the body, in world metres: the tips stay where they were when the body moves,
    /// then a damped spring pulls them back over it. Starts, stops and hops swing the pile; a steady walk combs it back.
    /// The lag is clamped, so a relocation pop or a teleport cannot fling the tips off the body.
    /// </summary>
    public sealed class FurInertia
    {
        /// <summary>Longer frames are integrated in steps of this, s: the spring stays stable through a hitch.</summary>
        const float MaxStep = 1f / 60f;

        readonly float _stiffness;
        readonly float _damping;
        readonly float _maxOffset;
        Vector3 _anchor;
        Vector3 _velocity;
        bool _started;

        /// <param name="frequency">Natural frequency of the swing, Hz.</param>
        /// <param name="dampingRatio">1 settles without overshoot; below that the tips bounce.</param>
        /// <param name="maxOffset">Longest lag, m.</param>
        public FurInertia(float frequency, float dampingRatio, float maxOffset)
        {
            var omega = 2f * Mathf.PI * Mathf.Max(frequency, 0.01f);
            _stiffness = omega * omega;
            _damping = 2f * Mathf.Max(dampingRatio, 0f) * omega;
            _maxOffset = Mathf.Max(maxOffset, 0f);
        }

        public Vector3 Offset { get; private set; }

        /// <summary>Moves the body to <paramref name="anchor"/> and advances the swing. The first call only places it.</summary>
        public Vector3 Step(Vector3 anchor, float deltaTime)
        {
            if (!_started)
            {
                _anchor = anchor;
                _started = true;
                return Offset;
            }
            var offset = Offset - (anchor - _anchor);
            _anchor = anchor;
            for (var left = Mathf.Max(deltaTime, 0f); left > 0f; left -= MaxStep)
            {
                var dt = Mathf.Min(left, MaxStep);
                _velocity += (-_stiffness * offset - _damping * _velocity) * dt;
                offset += _velocity * dt;
            }
            if (offset.sqrMagnitude > _maxOffset * _maxOffset)
            {
                offset = offset.normalized * _maxOffset;
                // Pinned at the limit: drop the speed that would carry it further out.
                var outward = Vector3.Dot(_velocity, offset);
                if (outward > 0f)
                    _velocity -= offset * (outward / offset.sqrMagnitude);
            }
            Offset = offset;
            return Offset;
        }
    }
}
