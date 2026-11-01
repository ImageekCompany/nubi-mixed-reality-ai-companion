using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// 30 s with Nubi idle (idle animations on, no conversation): frame rate, frame-time tail, render load (DoD §4),
    /// GC allocation and memory. "GC Allocated In Frame" is only recorded in development players; release builds report
    /// GC collections instead. A counter a player does not record reads NaN and is not checked.
    /// </summary>
    public static class PerfScenario
    {
        public const string Name = "perf_idle";
        const double SampleSeconds = 30;
        // The display runs at 72 Hz, so a clean run averages 72.0; half a frame per second absorbs timer jitter.
        const double MinAverageFps = 71.5;
        const long MaxDrawCalls = 200;
        const long MaxSetPassCalls = 60;
        const double MaxAllocBytesPerFrame = 4096;
        const double Mb = 1024 * 1024;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = new ScenarioResult(Name);
            await c.WaitForIdle(20, ct);
            var frames = new List<float>(4096);
            using var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            using var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            long maxDrawCalls = 0, maxSetPassCalls = 0, maxTriangles = 0;
            var collections = GC.CollectionCount(0);
            double allocated = 0;
            var end = c.Clock.Now + SampleSeconds;
            while (c.Clock.Now < end)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                frames.Add(Time.unscaledDeltaTime);
                if (gcAlloc.Valid)
                    allocated += gcAlloc.LastValue;
                maxDrawCalls = Math.Max(maxDrawCalls, drawCalls.Valid ? drawCalls.LastValue : 0);
                maxSetPassCalls = Math.Max(maxSetPassCalls, setPassCalls.Valid ? setPassCalls.LastValue : 0);
                maxTriangles = Math.Max(maxTriangles, triangles.Valid ? triangles.LastValue : 0);
            }

            var total = 0.0;
            foreach (var dt in frames)
                total += dt;
            frames.Sort();
            var averageFps = frames.Count / total;
            var allocPerFrame = gcAlloc.Valid ? allocated / frames.Count : double.NaN;
            r.Metric("frames", frames.Count).Metric("avg_fps", averageFps)
             .Metric("p95_frame_ms", Percentile(frames, 0.95) * 1000).Metric("p99_frame_ms", Percentile(frames, 0.99) * 1000)
             .Metric("draw_calls_max", drawCalls.Valid ? maxDrawCalls : double.NaN)
             .Metric("setpass_calls_max", setPassCalls.Valid ? maxSetPassCalls : double.NaN)
             .Metric("triangles_max", triangles.Valid ? maxTriangles : double.NaN)
             .Metric("gc_alloc_bytes_per_frame", allocPerFrame)
             .Metric("gc_collections", GC.CollectionCount(0) - collections)
             .Metric("system_memory_mb", systemMemory.Valid ? systemMemory.LastValue / Mb : double.NaN)
             .Metric("mono_heap_mb", UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / Mb);
            r.Check(averageFps >= MinAverageFps, "average frame rate below 71.5 fps (72 Hz display)");
            r.Check(!drawCalls.Valid || maxDrawCalls <= MaxDrawCalls, "more than 200 draw calls in a frame");
            r.Check(!setPassCalls.Valid || maxSetPassCalls <= MaxSetPassCalls, "more than 60 SetPass calls in a frame");
            r.Check(!(allocPerFrame > MaxAllocBytesPerFrame), "more than 4 KB GC allocation per frame while idle");
            if (allocPerFrame > 0)
                r.Degrade("idle allocates (target 0 B/frame)");
            return r;
        }

        static double Percentile(List<float> sorted, double p) =>
            sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
    }
}
