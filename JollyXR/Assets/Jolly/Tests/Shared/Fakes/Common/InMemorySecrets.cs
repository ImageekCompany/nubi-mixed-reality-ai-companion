using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Testing
{
    /// <summary>ISecretsProvider over a dictionary. Empty values count as absent, as in the real providers.</summary>
    public sealed class InMemorySecrets : ISecretsProvider
    {
        readonly Dictionary<string, string> _values = new();

        public InMemorySecrets With(string key, string value)
        {
            _values[key] = value;
            return this;
        }

        public bool TryGet(string key, out string value)
        {
            if (_values.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
                return true;
            value = null;
            return false;
        }
    }
}
