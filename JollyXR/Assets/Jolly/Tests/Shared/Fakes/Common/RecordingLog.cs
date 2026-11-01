using System;
using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>ILog that keeps every entry at or above <see cref="MinimumLevel"/> for assertions.</summary>
    public sealed class RecordingLog : ILog
    {
        public readonly struct Entry
        {
            public Entry(LogLevel level, string category, string message, Exception exception)
            { Level = level; Category = category; Message = message; Exception = exception; }
            public LogLevel Level { get; }
            public string Category { get; }
            public string Message { get; }
            public Exception Exception { get; }
            public override string ToString() => $"{Level} [{Category}] {Message}";
        }

        readonly List<Entry> _entries = new();

        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;
        public IReadOnlyList<Entry> Entries => _entries;

        public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

        public void Write(LogLevel level, string category, string message, Exception exception = null)
        {
            if (IsEnabled(level))
                _entries.Add(new Entry(level, category, message, exception));
        }

        public bool Contains(LogLevel level, string messageFragment)
        {
            foreach (var e in _entries)
            {
                if (e.Level == level && e.Message != null && e.Message.Contains(messageFragment, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public void Clear() => _entries.Clear();
    }
}
