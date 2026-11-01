using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Jolly.Contracts;

namespace Jolly.Core.Diagnostics
{
    /// <summary>
    /// Defence in depth in front of the log sink: masks Meta API keys, bearer tokens and the configured secret values.
    /// Callers must still never log secrets; this only limits the damage when one slips through. Exceptions are folded
    /// into the message and redacted too: provider errors carry the server's message verbatim, which can echo input.
    /// </summary>
    public sealed class RedactingLog : ILog
    {
        const string Mask = "***";
        const int MinSecretLength = 8;
        static readonly Regex KeyPattern = new(@"LLM_\d+_\S+|(?<=Bearer )\S+", RegexOptions.CultureInvariant);

        readonly ILog _inner;
        readonly List<string> _secrets = new();

        public RedactingLog(ILog inner, IEnumerable<string> secretValues)
        {
            _inner = inner;
            foreach (var value in secretValues)
            {
                if (value != null && value.Length >= MinSecretLength)
                    _secrets.Add(value);
            }
        }

        public bool IsEnabled(LogLevel level) => _inner.IsEnabled(level);

        public void Write(LogLevel level, string category, string message, Exception exception = null)
        {
            if (!_inner.IsEnabled(level))
                return;
            if (exception != null)
                message = message + "\n" + exception;
            _inner.Write(level, category, Redact(message));
        }

        string Redact(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;
            foreach (var secret in _secrets)
                message = message.Replace(secret, Mask, StringComparison.Ordinal);
            return KeyPattern.Replace(message, Mask);
        }
    }
}
