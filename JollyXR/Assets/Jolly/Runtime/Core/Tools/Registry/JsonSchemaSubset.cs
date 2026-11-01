using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Parser and validator for the JSON Schema subset of contracts §4.1. The server does not enforce <c>strict</c>, so every
    /// model call is validated here before a tool runs; failures are model-facing messages.
    /// </summary>
    public static class JsonSchemaSubset
    {
        static readonly HashSet<string> RootKeys = new() { "type", "properties", "required", "additionalProperties" };
        static readonly HashSet<string> PropertyKeys = new() { "type", "description", "enum", "maxLength", "minimum", "maximum" };

        /// <summary>Throws <see cref="ArgumentException"/> for anything outside the subset.</summary>
        public static ToolSchema Parse(string schemaJson)
        {
            var root = ParseObject(schemaJson) ?? throw new ArgumentException("schema is not a JSON object");
            RequireOnly(root, RootKeys, "schema");
            if ((string)root["type"] != "object")
                throw new ArgumentException("schema type must be 'object'");
            if (root["additionalProperties"]?.Type != JTokenType.Boolean || (bool)root["additionalProperties"])
                throw new ArgumentException("schema must set additionalProperties:false");
            if (root["properties"] is not JObject properties)
                throw new ArgumentException("schema needs a 'properties' object");

            var required = new HashSet<string>();
            if (root["required"] is JArray names)
            {
                foreach (var name in names)
                    required.Add((string)name);
            }

            var parsed = new List<PropertySchema>();
            foreach (var property in properties.Properties())
            {
                parsed.Add(ParseProperty(property, required.Remove(property.Name)));
            }
            if (required.Count > 0)
                throw new ArgumentException("required names an undeclared property");
            return new ToolSchema(parsed);
        }

        public static bool Validate(ToolSchema schema, string argumentsJson, out ToolArguments args, out string error)
        {
            args = null;
            JObject root;
            try
            {
                root = string.IsNullOrWhiteSpace(argumentsJson) ? new JObject() : ParseObject(argumentsJson);
            }
            catch (ArgumentException)
            {
                error = "arguments are not valid JSON";
                return false;
            }
            if (root == null)
            {
                error = "arguments must be a JSON object";
                return false;
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var member in root.Properties())
            {
                var property = schema.Find(member.Name);
                if (property == null)
                {
                    error = $"unknown property '{member.Name}'";
                    return false;
                }
                error = Check(property, member.Value, out var value);
                if (error != null)
                    return false;
                values[member.Name] = value;
            }
            foreach (var property in schema.Properties)
            {
                if (property.Required && !values.ContainsKey(property.Name))
                {
                    error = $"missing required property '{property.Name}'";
                    return false;
                }
            }
            args = new ToolArguments(values);
            error = null;
            return true;
        }

        static string Check(PropertySchema p, JToken token, out object value)
        {
            value = null;
            switch (p.Type)
            {
                case PropertyType.String when token.Type == JTokenType.String:
                    var text = (string)token;
                    if (p.MaxLength is { } max && text.Length > max)
                        return $"property '{p.Name}' is longer than {max} characters";
                    if (p.Allowed != null && !Contains(p.Allowed, text))
                        return $"property '{p.Name}' must be one of: {string.Join(", ", p.Allowed)}";
                    value = text;
                    return null;
                case PropertyType.Number when token.Type is JTokenType.Integer or JTokenType.Float:
                    var number = (double)token;
                    if (p.Minimum is { } min && number < min)
                        return $"property '{p.Name}' must be at least {Format(min)}";
                    if (p.Maximum is { } maxValue && number > maxValue)
                        return $"property '{p.Name}' must be at most {Format(maxValue)}";
                    value = number;
                    return null;
                case PropertyType.Boolean when token.Type == JTokenType.Boolean:
                    value = (bool)token;
                    return null;
                default:
                    return $"property '{p.Name}' must be a {p.Type.ToString().ToLowerInvariant()}";
            }
        }

        static PropertySchema ParseProperty(JProperty property, bool required)
        {
            if (property.Value is not JObject p)
                throw new ArgumentException($"property '{property.Name}' is not an object");
            RequireOnly(p, PropertyKeys, property.Name);
            var type = (string)p["type"] switch
            {
                "string" => PropertyType.String,
                "number" => PropertyType.Number,
                "boolean" => PropertyType.Boolean,
                var other => throw new ArgumentException($"property '{property.Name}' has unsupported type '{other}'"),
            };
            List<string> allowed = null;
            if (p["enum"] is JArray values)
            {
                allowed = new List<string>();
                foreach (var v in values)
                    allowed.Add((string)v);
            }
            return new PropertySchema(property.Name, type, required, allowed,
                (int?)p["maxLength"], (double?)p["minimum"], (double?)p["maximum"]);
        }

        static void RequireOnly(JObject obj, HashSet<string> allowed, string where)
        {
            foreach (var member in obj.Properties())
            {
                if (!allowed.Contains(member.Name))
                    throw new ArgumentException($"'{member.Name}' in {where} is outside the supported schema subset");
            }
        }

        static JObject ParseObject(string json)
        {
            try
            {
                return JToken.Parse(json) as JObject;
            }
            catch (JsonException e)
            {
                throw new ArgumentException("invalid JSON", e);
            }
        }

        static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (var item in list)
            {
                if (item == value)
                    return true;
            }
            return false;
        }

        static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
