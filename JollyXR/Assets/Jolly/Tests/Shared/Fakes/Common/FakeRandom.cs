using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>
    /// Deterministic IRandom. Scripted values are returned first, in order (clamped to the requested range);
    /// after that a System.Random with a fixed seed takes over.
    /// </summary>
    public sealed class FakeRandom : IRandom
    {
        readonly Queue<float> _floats = new();
        readonly Queue<int> _ints = new();
        readonly Random _seeded;

        public FakeRandom(int seed = 1234) => _seeded = new Random(seed);

        public FakeRandom EnqueueFloat(params float[] values)
        {
            foreach (var v in values)
                _floats.Enqueue(v);
            return this;
        }

        public FakeRandom EnqueueInt(params int[] values)
        {
            foreach (var v in values)
                _ints.Enqueue(v);
            return this;
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            if (_floats.Count > 0)
                return Math.Clamp(_floats.Dequeue(), minInclusive, maxInclusive);
            return minInclusive + (float)_seeded.NextDouble() * (maxInclusive - minInclusive);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (_ints.Count > 0)
                return maxExclusive <= minInclusive ? minInclusive : Math.Clamp(_ints.Dequeue(), minInclusive, maxExclusive - 1);
            return _seeded.Next(minInclusive, maxExclusive);
        }
    }
}
