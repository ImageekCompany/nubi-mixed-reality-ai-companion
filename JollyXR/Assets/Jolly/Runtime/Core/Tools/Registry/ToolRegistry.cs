using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Tools by name, each schema parsed once at <see cref="Register"/>. A persona's null <c>AllowedTools</c> means every tool;
    /// seated mode withholds <c>walk_to</c>, <c>go_to_surface</c> and <c>follow_user</c> but keeps <c>come_here</c> (architecture §5.5).
    /// </summary>
    public sealed class ToolRegistry : IToolRegistry
    {
        static readonly Regex NamePattern = new("^[a-zA-Z0-9_.-]+$");

        sealed class Entry
        {
            public Entry(ITool tool, ToolSchema schema) { Tool = tool; Schema = schema; }
            public ITool Tool { get; }
            public ToolSchema Schema { get; }
        }

        readonly List<Entry> _ordered = new();
        readonly Dictionary<string, Entry> _byName = new(StringComparer.Ordinal);

        public void Register(ITool tool)
        {
            var definition = tool.Definition;
            if (definition.Name == null || !NamePattern.IsMatch(definition.Name))
                throw new ArgumentException($"invalid tool name '{definition.Name}'");
            if (_byName.ContainsKey(definition.Name))
                throw new InvalidOperationException($"tool '{definition.Name}' is already registered");

            var entry = new Entry(tool, JsonSchemaSubset.Parse(definition.ParametersJsonSchema));
            _ordered.Add(entry);
            _byName.Add(definition.Name, entry);
        }

        public bool TryGet(string name, out ITool tool)
        {
            tool = _byName.TryGetValue(name, out var entry) ? entry.Tool : null;
            return tool != null;
        }

        public IReadOnlyList<ToolDefinition> DefinitionsFor(PersonaProfile persona, IUserPreferences preferences)
        {
            var seated = preferences?.Current?.Seated ?? false;
            var definitions = new List<ToolDefinition>(_ordered.Count);
            foreach (var entry in _ordered)
            {
                var name = entry.Tool.Definition.Name;
                if (IsAllowed(persona?.AllowedTools, name) && !(seated && ToolNames.IsWithheldWhenSeated(name)))
                    definitions.Add(entry.Tool.Definition);
            }
            return definitions;
        }

        public bool TryValidate(string name, string argumentsJson, out IToolArguments args, out string error)
        {
            args = null;
            if (!_byName.TryGetValue(name, out var entry))
            {
                error = $"unknown tool '{name}'";
                return false;
            }
            var ok = JsonSchemaSubset.Validate(entry.Schema, argumentsJson, out var parsed, out error);
            args = parsed;
            return ok;
        }

        static bool IsAllowed(IReadOnlyList<string> allowed, string name)
        {
            if (allowed == null)
                return true;
            foreach (var a in allowed)
            {
                if (a == name)
                    return true;
            }
            return false;
        }
    }
}
