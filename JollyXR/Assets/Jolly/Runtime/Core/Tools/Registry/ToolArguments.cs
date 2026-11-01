using System.Collections.Generic;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>Validated arguments; typed getters never convert between types.</summary>
    public sealed class ToolArguments : IToolArguments
    {
        public static readonly ToolArguments Empty = new(new Dictionary<string, object>());

        readonly IReadOnlyDictionary<string, object> _values;

        public ToolArguments(IReadOnlyDictionary<string, object> values) => _values = values;

        public bool TryGetString(string name, out string value) => TryGet(name, out value);
        public bool TryGetNumber(string name, out double value) => TryGet(name, out value);
        public bool TryGetBool(string name, out bool value) => TryGet(name, out value);

        bool TryGet<T>(string name, out T value)
        {
            if (_values.TryGetValue(name, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
    }
}
