using System;
using System.Globalization;
using System.Text;
using Jolly.Contracts;

namespace Jolly.Core.Diagnostics
{
    /// <summary>
    /// Per-turn latency marks. A turn is finalised by <see cref="LatencyMark.TurnCompleted"/> or by the next
    /// <see cref="BeginTurn"/> (barge-in), and then emits one <c>evt=turn.latency</c> line that the DoD scripts parse.
    /// Only the first occurrence of each mark counts.
    /// </summary>
    public sealed class LatencyTracker : ILatencyTracker
    {
        static readonly LatencyMark[] Marks = (LatencyMark[])Enum.GetValues(typeof(LatencyMark));

        readonly IClock _clock;
        readonly ILog _log;
        readonly double[] _current = NewMarks();
        readonly double[] _completed = NewMarks();
        readonly StringBuilder _line = new(256);
        int _turnId = -1;
        bool _open;

        public LatencyTracker(IClock clock, ILog log)
        {
            _clock = clock;
            _log = log;
        }

        public void BeginTurn(int turnId)
        {
            Finalise();
            _turnId = turnId;
            _open = true;
            Array.Fill(_current, double.NaN);
        }

        public void Mark(int turnId, LatencyMark mark)
        {
            if (!_open || turnId != _turnId || !double.IsNaN(_current[(int)mark]))
                return;
            _current[(int)mark] = _clock.Now;
            if (mark == LatencyMark.TurnCompleted)
                Finalise();
        }

        public double Between(LatencyMark from, LatencyMark to) =>
            (_completed[(int)to] - _completed[(int)from]) * 1000.0;

        void Finalise()
        {
            if (!_open)
                return;
            _open = false;
            Array.Copy(_current, _completed, _current.Length);
            if (_log.IsEnabled(LogLevel.Info))
                _log.Write(LogLevel.Info, "latency", FormatLine());
        }

        string FormatLine()
        {
            var origin = double.NaN;
            foreach (var t in _completed)
            {
                if (!double.IsNaN(t) && (double.IsNaN(origin) || t < origin))
                    origin = t;
            }

            _line.Clear().Append("evt=turn.latency turn=").Append(_turnId);
            foreach (var mark in Marks)
            {
                var t = _completed[(int)mark];
                if (double.IsNaN(t))
                    continue;
                _line.Append(' ').Append(mark).Append('=')
                     .Append(Math.Round((t - origin) * 1000.0).ToString(CultureInfo.InvariantCulture));
            }
            return _line.ToString();
        }

        static double[] NewMarks()
        {
            var marks = new double[Enum.GetValues(typeof(LatencyMark)).Length];
            Array.Fill(marks, double.NaN);
            return marks;
        }
    }
}
