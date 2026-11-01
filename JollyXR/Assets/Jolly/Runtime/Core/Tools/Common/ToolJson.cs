using System.Globalization;
using System.Text;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>Builds a tool's flat <c>output</c> object; strings are escaped with <see cref="ToolResult.JsonString"/>.</summary>
    public sealed class ToolJson
    {
        readonly StringBuilder _sb = new StringBuilder(64).Append('{');

        public ToolJson Add(string name, string value) => Raw(name, value == null ? "null" : ToolResult.JsonString(value));
        public ToolJson Add(string name, int value) => Raw(name, value.ToString(CultureInfo.InvariantCulture));
        public ToolJson Add(string name, double value) => Raw(name, value.ToString("0.##", CultureInfo.InvariantCulture));
        public ToolJson Add(string name, bool value) => Raw(name, value ? "true" : "false");

        public override string ToString() => _sb.ToString() + "}";

        ToolJson Raw(string name, string json)
        {
            if (_sb.Length > 1)
                _sb.Append(',');
            _sb.Append(ToolResult.JsonString(name)).Append(':').Append(json);
            return this;
        }
    }
}
