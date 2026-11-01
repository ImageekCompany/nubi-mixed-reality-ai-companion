using System;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Diagnostics
{
    /// <summary>The ILog sink over <c>Debug.unityLogger</c>; wrap it in <see cref="RedactingLog"/>.</summary>
    public sealed class UnityLog : ILog
    {
        readonly LogLevel _minimum;

        public UnityLog(LogLevel minimum) => _minimum = minimum;

        public bool IsEnabled(LogLevel level) => level >= _minimum;

        public void Write(LogLevel level, string category, string message, Exception exception = null)
        {
            if (!IsEnabled(level))
                return;
            Debug.unityLogger.Log(ToLogType(level), category, message);
            if (exception != null)
                Debug.unityLogger.LogException(exception);
        }

        static LogType ToLogType(LogLevel level) => level switch
        {
            LogLevel.Error => LogType.Error,
            LogLevel.Warning => LogType.Warning,
            _ => LogType.Log,
        };
    }
}
