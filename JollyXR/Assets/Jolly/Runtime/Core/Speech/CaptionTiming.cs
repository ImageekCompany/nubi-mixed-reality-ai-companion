using System;

namespace Jolly.Core.Speech
{
    /// <summary>How long a caption with no audio stays up: about reading speed, never a flash, never a wall.</summary>
    public static class CaptionTiming
    {
        const double SecondsPerChar = 0.06;
        const double MinSeconds = 1.5;
        const double MaxSeconds = 6;

        public static TimeSpan HoldFor(string text) =>
            TimeSpan.FromSeconds(Math.Clamp((text?.Length ?? 0) * SecondsPerChar, MinSeconds, MaxSeconds));
    }
}
