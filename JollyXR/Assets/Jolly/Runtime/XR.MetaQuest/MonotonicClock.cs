using UnityEngine;

namespace Jolly.XR.MetaQuest
{
    /// <summary>
    /// XRCpuImage.timestamp on Quest is CLOCK_MONOTONIC seconds (it tracks SystemClock.uptimeMillis, not
    /// elapsedRealtime: device-spike.md §1). This maps it onto Time.realtimeSinceStartupAsDouble, the time base of
    /// the head-pose history.
    /// </summary>
    static class MonotonicClock
    {
        /// <summary>CLOCK_MONOTONIC minus realtimeSinceStartup, sampled now; NaN off-device.</summary>
        public static double OffsetFromRealtime()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var clock = new AndroidJavaClass("android.os.SystemClock");
            return clock.CallStatic<long>("uptimeMillis") / 1000.0 - Time.realtimeSinceStartupAsDouble;
#else
            return double.NaN;
#endif
        }
    }
}
