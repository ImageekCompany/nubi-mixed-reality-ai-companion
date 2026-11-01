using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Testing
{
    public sealed class FakeLatencyTracker : ILatencyTracker
    {
        readonly Dictionary<(LatencyMark, LatencyMark), double> _between = new();

        public void Set(LatencyMark from, LatencyMark to, double ms) => _between[(from, to)] = ms;
        public void BeginTurn(int turnId) { }
        public void Mark(int turnId, LatencyMark mark) { }
        public double Between(LatencyMark from, LatencyMark to) => _between.TryGetValue((from, to), out var ms) ? ms : double.NaN;
    }
}
