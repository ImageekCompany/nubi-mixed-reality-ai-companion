using System;
using UnityEngine.Profiling;

namespace Jolly.Tests.EditMode.Net
{
    /// <summary>
    /// Counts managed allocations on this thread, as the Test Framework's <c>Is.Not.AllocatingGCMemory()</c> does, but
    /// returns the number so a test can allow the allocations it expects. Counts, not bytes: the editor's
    /// GC.GetAllocatedBytesForCurrentThread reads 0.
    /// </summary>
    static class GcAllocations
    {
        public static int Count(Action action)
        {
            var recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try
            {
                action();
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }
            return recorder.sampleBlockCount;
        }
    }
}
