using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.App.Platform
{
    /// <summary>
    /// The runtime <see cref="IClock"/>. <see cref="Now"/> reads a Stopwatch, so it is monotonic and safe on any thread
    /// (audio and socket threads read it too); delays run on the PlayerLoop in real time, unaffected by timeScale.
    /// </summary>
    public sealed class UnityClock : IClock
    {
        readonly long _origin = Stopwatch.GetTimestamp();

        public double Now => (Stopwatch.GetTimestamp() - _origin) / (double)Stopwatch.Frequency;
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public UniTask Delay(TimeSpan delay, CancellationToken ct) =>
            UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, ct);
    }
}
