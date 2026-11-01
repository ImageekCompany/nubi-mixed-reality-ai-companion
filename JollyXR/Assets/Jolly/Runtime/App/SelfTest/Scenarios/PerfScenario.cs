using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// 30 s with Nubi idle (idle animations on, no conversation): frame rate, frame-time tail, GC allocation and memory.
    /// "GC Allocated In Frame" is only recorded in development players; release builds report GC collections instead.
    /// </summary>
    public static class PerfScenario
    {
        public const string Name = "perf_idle";
        const double SampleSeconds = 30;
        const double MinAverageFps = 68;
        const double MaxAllocBytesPerFrame = 4096;
        const double Mb = 1024 * 1024;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = new ScenarioResult(Name);
            await c.WaitForIdle(20, ct);
            var frames = new List<float>(4096);
            using var gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            using var systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            var collections = GC.CollectionCount(0);
            double allocated = 0;
            var end = c.Clock.Now + SampleSeconds;
            while (c.Clock.Now < end)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                frames.Add(Time.unscaledDeltaTime);
                if (gcAlloc.Valid)
                    allocated += gcAlloc.LastValue;
            }

            var total = 0.0;
            foreach (var dt in frames)
                total += dt;
            frames.Sort();
            var averageFps = frames.Count / total;
            var allocPerFrame = gcAlloc.Valid ? allocated / frames.Count : double.NaN;
            r.Metric("frames", frames.Count).Metric("avg_fps", averageFps)
             .Metric("p95_frame_ms", Percentile(frames, 0.95) * 1000).Metric("p99_frame_ms", Percentile(frames, 0.99) * 1000)
             .Metric("gc_alloc_bytes_per_frame", allocPerFrame)
             .Metric("gc_collections", GC.CollectionCount(0) - collections)
             .Metric("system_memory_mb", systemMemory.Valid ? systemMemory.LastValue / Mb : double.NaN)
             .Metric("mono_heap_mb", UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / Mb);
            r.Check(averageFps >= MinAverageFps, "average frame rate below 68 fps");
            r.Check(!(allocPerFrame > MaxAllocBytesPerFrame), "more than 4 KB GC allocation per frame while idle");
            if (allocPerFrame > 0)
                r.Degrade("idle allocates (target 0 B/frame)");
            return r;
        }

        static double Percentile(List<float> sorted, double p) =>
            sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(p * sorted.Count))];
    }
}
