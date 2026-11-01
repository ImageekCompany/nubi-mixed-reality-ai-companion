using Jolly.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jolly.App.Platform
{
    /// <summary>
    /// Starts pipeline creation for the recorded graphics states on Unity's worker threads at boot. Vulkan builds a
    /// pipeline at its first draw, so without this the first caption, badge, mask and Nubi pose each stalled the render
    /// thread mid-turn (device runs 2026-11-01). The collection is recorded by a development-player self-test
    /// (docs/perf/README.md §7) and is only valid for the graphics API it was recorded on.
    /// </summary>
    public static class GraphicsWarmup
    {
        public static void Start(GraphicsStateCollection states, ILog log)
        {
            if (states == null)
            {
                log.Write(LogLevel.Info, "perf", "evt=gfx.warmup skipped=no_collection");
                return;
            }
            if (states.graphicsDeviceType != SystemInfo.graphicsDeviceType)
            {
                log.Write(LogLevel.Info, "perf",
                    $"evt=gfx.warmup skipped=api recorded={states.graphicsDeviceType} running={SystemInfo.graphicsDeviceType}");
                return;
            }
            states.WarmUp();
            log.Write(LogLevel.Info, "perf",
                $"evt=gfx.warmup variants={states.variantCount} states={states.totalGraphicsStateCount}");
        }
    }
}
