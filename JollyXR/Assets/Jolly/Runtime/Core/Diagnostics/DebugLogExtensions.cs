using System.Diagnostics;
using Jolly.Contracts;

namespace Jolly.Core.Diagnostics
{
    public static class DebugLogExtensions
    {
        /// <summary>
        /// The only way to log user transcripts or model text. Without <c>JOLLY_DEBUG_LOG</c> the compiler removes the call
        /// and the evaluation of its arguments. [Conditional] cannot sit on ILog itself (CS0582/CS0629).
        /// </summary>
        [Conditional("JOLLY_DEBUG_LOG")]
        public static void LogDebug(this ILog log, string category, string message) =>
            log.Write(LogLevel.Debug, category, message);
    }
}
