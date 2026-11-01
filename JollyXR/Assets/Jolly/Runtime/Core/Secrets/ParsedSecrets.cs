using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Secrets
{
    /// <summary>A secrets file parsed once at construction. Empty values count as absent.</summary>
    public abstract class ParsedSecrets : ISecretsProvider
    {
        readonly Dictionary<string, string> _values = new();

        protected void Set(string key, string value)
        {
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                _values[key] = value;
        }

        public bool TryGet(string key, out string value) => _values.TryGetValue(key, out value);
    }
}
