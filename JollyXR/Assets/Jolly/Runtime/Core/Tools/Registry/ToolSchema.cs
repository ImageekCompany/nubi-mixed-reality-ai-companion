using System.Collections.Generic;

namespace Jolly.Core.Tools
{
    public enum PropertyType { String, Number, Boolean }

    /// <summary>One property of the contracts §4.1 subset: string (enum, maxLength), number (minimum, maximum) or boolean.</summary>
    public sealed class PropertySchema
    {
        public PropertySchema(string name, PropertyType type, bool required, IReadOnlyList<string> allowed,
                              int? maxLength, double? minimum, double? maximum)
        {
            Name = name; Type = type; Required = required; Allowed = allowed;
            MaxLength = maxLength; Minimum = minimum; Maximum = maximum;
        }

        public string Name { get; }
        public PropertyType Type { get; }
        public bool Required { get; }
        public IReadOnlyList<string> Allowed { get; }   // null = any string
        public int? MaxLength { get; }
        public double? Minimum { get; }
        public double? Maximum { get; }
    }

    /// <summary>A parsed flat object schema; properties keep their declaration order.</summary>
    public sealed class ToolSchema
    {
        public ToolSchema(IReadOnlyList<PropertySchema> properties) => Properties = properties;

        public IReadOnlyList<PropertySchema> Properties { get; }

        public PropertySchema Find(string name)
        {
            foreach (var p in Properties)
            {
                if (p.Name == name)
                    return p;
            }
            return null;
        }
    }
}
