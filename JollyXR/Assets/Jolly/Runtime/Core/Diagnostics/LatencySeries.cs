using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jolly.Core.Diagnostics
{
    /// <summary>
    /// One latency stage over many turns, in milliseconds. Percentiles use the nearest rank, so every reported value is a
    /// turn that happened (p50 of 20 is the 10th fastest). A NaN sample, a turn without one of the stage's marks, is skipped.
    /// </summary>
    public sealed class LatencySeries
    {
        readonly List<double> _values = new();
        bool _sorted = true;

        public int Count => _values.Count;
        public double Max => Percentile(100);

        public void Add(double ms)
        {
            if (double.IsNaN(ms))
                return;
            _values.Add(ms);
            _sorted = false;
        }

        /// <summary>NaN when empty.</summary>
        public double Percentile(double p)
        {
            if (_values.Count == 0)
                return double.NaN;
            if (!_sorted)
            {
                _values.Sort();
                _sorted = true;
            }
            var rank = (int)Math.Ceiling(p * _values.Count / 100);
            return _values[Math.Clamp(rank, 1, _values.Count) - 1];
        }

        /// <summary><c>n=20 p50=1776 p90=2412 max=3165</c>; <c>n=0</c> when empty.</summary>
        public string Summary() =>
            Count == 0
                ? "n=0"
                : string.Format(CultureInfo.InvariantCulture, "n={0} p50={1:0} p90={2:0} max={3:0}",
                                Count, Percentile(50), Percentile(90), Max);
    }
}
