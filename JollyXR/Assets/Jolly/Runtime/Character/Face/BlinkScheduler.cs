using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Character
{
    /// <summary>Random blinks every 2–6 s, a double blink 15% of the time (architecture §5.5). Pure: time comes in as dt.</summary>
    public sealed class BlinkScheduler
    {
        readonly IRandom _random;
        readonly FaceSettings _s;
        float _untilNext;
        float _phase = -1f;
        bool _doublePending;
        bool _nextIsSecond;

        public BlinkScheduler(IRandom random, FaceSettings settings)
        {
            _random = random;
            _s = settings;
            _untilNext = NextInterval();
        }

        float Duration => _s.blinkCloseSeconds + _s.blinkHoldSeconds + _s.blinkOpenSeconds;

        /// <summary>Advances time and returns the lid closure, 0 (open) to 1 (closed).</summary>
        public float Tick(float deltaTime)
        {
            if (_phase < 0f)
            {
                _untilNext -= deltaTime;
                if (_untilNext > 0f)
                    return 0f;
                Begin(-_untilNext);
            }
            else
            {
                _phase += deltaTime;
            }

            if (_phase < Duration)
                return Closure(_phase);

            _phase = -1f;
            _nextIsSecond = _doublePending;
            _untilNext = _doublePending ? _s.doubleBlinkGap : NextInterval();
            _doublePending = false;
            return 0f;
        }

        /// <summary>Blinks on the next tick (idle blink burst).</summary>
        public void BlinkNow()
        {
            if (_phase < 0f)
                _untilNext = 0f;
        }

        void Begin(float overshoot)
        {
            _doublePending = !_nextIsSecond && _random.Range(0f, 1f) < _s.doubleBlinkChance;
            _nextIsSecond = false;
            _phase = overshoot;
        }

        float Closure(float t)
        {
            if (t < _s.blinkCloseSeconds)
                return Mathf.SmoothStep(0f, 1f, t / _s.blinkCloseSeconds);
            t -= _s.blinkCloseSeconds;
            if (t < _s.blinkHoldSeconds)
                return 1f;
            t -= _s.blinkHoldSeconds;
            return 1f - Mathf.SmoothStep(0f, 1f, t / _s.blinkOpenSeconds);
        }

        float NextInterval() => _random.Range(_s.blinkIntervalMin, _s.blinkIntervalMax);
    }
}
