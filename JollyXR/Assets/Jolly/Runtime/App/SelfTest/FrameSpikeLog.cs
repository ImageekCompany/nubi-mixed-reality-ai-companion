using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Unity.Profiling;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// Self-test frame diagnostics for the stale-frame row of DoD §4: one <c>evt=frame.spike</c> line per main-thread
    /// frame over <see cref="SpikeSeconds"/>, so a hitch can be matched to the events logged around it, and per-phase
    /// windows (<see cref="TakeWindow"/>). GC bytes are recorded only by development players; release players log NaN.
    /// </summary>
    public sealed class FrameSpikeLog : IDisposable
    {
        // Two 72 Hz frames: anything longer makes the compositor show at least one stale frame.
        const double SpikeSeconds = 0.028;

        readonly ILog _log;
        ProfilerRecorder _gcAlloc;
        long _frames, _spikes, _gcBytes;
        double _worstMs;

        public FrameSpikeLog(ILog log)
        {
            _log = log;
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        /// <summary>Frames, spikes, worst frame and GC bytes per frame since the last call.</summary>
        public string TakeWindow()
        {
            var gc = _gcAlloc.Valid && _frames > 0 ? (double)_gcBytes / _frames : double.NaN;
            var window = string.Format(CultureInfo.InvariantCulture,
                "frames={0} spikes={1} worst_ms={2:0.0} gc_bytes_per_frame={3:0}", _frames, _spikes, _worstMs, gc);
            _frames = _spikes = _gcBytes = 0;
            _worstMs = 0;
            return window;
        }

        /// <summary>Runs until <paramref name="ct"/> cancels, then disposes the recorder.</summary>
        public async UniTaskVoid Run(CancellationToken ct)
        {
            using var self = this;
            var last = Time.realtimeSinceStartupAsDouble;
            while (!await UniTask.Yield(PlayerLoopTiming.Initialization, ct).SuppressCancellationThrow())
            {
                var now = Time.realtimeSinceStartupAsDouble;
                var seconds = now - last;
                last = now;
                _frames++;
                var gcBytes = _gcAlloc.Valid ? _gcAlloc.LastValue : 0;
                _gcBytes += gcBytes;
                _worstMs = Math.Max(_worstMs, seconds * 1000);
                if (seconds <= SpikeSeconds)
                    continue;
                _spikes++;
                _log.Write(LogLevel.Info, "perf", string.Format(CultureInfo.InvariantCulture,
                    "evt=frame.spike ms={0:0.0} frame={1} gc_bytes={2}", seconds * 1000, Time.frameCount - 1, gcBytes));
            }
        }

        public void Dispose() => _gcAlloc.Dispose();
    }
}
