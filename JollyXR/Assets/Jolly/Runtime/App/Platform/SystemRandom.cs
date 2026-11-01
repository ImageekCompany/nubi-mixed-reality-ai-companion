using System;
using Jolly.Contracts;

namespace Jolly.App.Platform
{
    /// <summary>The runtime <see cref="IRandom"/>. Main thread only, like everything that schedules behaviour.</summary>
    public sealed class SystemRandom : IRandom
    {
        readonly Random _random;

        public SystemRandom(int seed) => _random = new Random(seed);

        public float Range(float minInclusive, float maxInclusive) =>
            minInclusive + (float)_random.NextDouble() * (maxInclusive - minInclusive);

        public int Range(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : _random.Next(minInclusive, maxExclusive);
    }
}
