using System;

namespace Jolly.Core.Connectivity
{
    /// <summary>Doubling delay between health probes: minimum, 2x, 4x, ... capped at maximum.</summary>
    public sealed class ProbeBackoff
    {
        const int MaxExponent = 16;

        readonly TimeSpan _min;
        readonly TimeSpan _max;
        int _attempt;

        public ProbeBackoff(TimeSpan min, TimeSpan max)
        {
            if (min <= TimeSpan.Zero || max < min)
                throw new ArgumentOutOfRangeException(nameof(min), "Need 0 < min <= max.");
            _min = min;
            _max = max;
        }

        public TimeSpan Next()
        {
            var delay = TimeSpan.FromTicks(Math.Min(_max.Ticks, _min.Ticks << _attempt));
            if (_attempt < MaxExponent)
                _attempt++;
            return delay;
        }

        public void Reset() => _attempt = 0;
    }
}
