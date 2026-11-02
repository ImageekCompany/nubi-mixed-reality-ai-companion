using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Tools by name, each schema parsed once at <see cref="Register"/>. A persona's null <c>AllowedTools</c> means every tool;
    /// seated mode withholds <c>walk_to</c>, <c>go_to_surface</c>, <c>follow_user</c> and <c>actions.move_relative</c> but keeps <c>come_here</c> (architecture §5.5).
    /// </summary>
    public sealed class ToolRegistry : IToolRegistry
    {
        static readonly Regex NamePattern = new("^[a-zA-Z0-9_.-]+$");
        const string ReservedNamespace = "browser";

        sealed class Entry
        {
            public Entry(ITool tool, ToolSchema schema) { Tool = tool; Schema = schema; }
            public ITool Tool { get; }
            public ToolSchema Schema { get; }
        }

        readonly List<Entry> _ordered = new();
        readonly Dictionary<string, Entry> _byName = new(StringComparer.Ordinal);
        readonly List<ToolNamespace> _namespaces = new();

        public void RegisterNamespace(ToolNamespace toolNamespace)
        {
            if (toolNamespace?.Name == null || toolNamespace.Name.Contains('.') || !NamePattern.IsMatch(toolNamespace.Name))
                throw new ArgumentException($"invalid tool namespace '{toolNamespace?.Name}'");
            // web_search injects browser.search, .open and .find; a request with one of those names fails with 400.
            if (toolNamespace.Name == ReservedNamespace)
                throw new ArgumentException($"tool namespace '{ReservedNamespace}' is reserved by web search");
            if (FindNamespace(toolNamespace.Name) != null)
                throw new InvalidOperationException($"tool namespace '{toolNamespace.Name}' is already registered");
            _namespaces.Add(toolNamespace);
        }

        public IReadOnlyList<ToolNamespace> NamespacesOf(IReadOnlyList<ToolDefinition> tools)
        {
            var used = new List<ToolNamespace>();
            foreach (var ns in _namespaces)
            {
                foreach (var tool in tools)
                {
                    if (NamespaceName(tool.Name) == ns.Name)
                    {
                        used.Add(ns);
                        break;
                    }
                }
            }
            return used;
        }

        public void Register(ITool tool)
        {
            var definition = tool.Definition;
            if (definition.Name == null || !NamePattern.IsMatch(definition.Name))
                throw new ArgumentException($"invalid tool name '{definition.Name}'");
            if (_byName.ContainsKey(definition.Name))
                throw new InvalidOperationException($"tool '{definition.Name}' is already registered");
            var ns = NamespaceName(definition.Name);
            if (ns != null && FindNamespace(ns) == null)
                throw new InvalidOperationException($"tool '{definition.Name}' needs namespace '{ns}' registered first");

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
                if (ToolNames.IsAllowed(persona?.AllowedTools, name) && !(seated && ToolNames.IsWithheldWhenSeated(name)))
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

        /// <summary>"ns" for "ns.fn"; null for a top-level name.</summary>
        static string NamespaceName(string toolName)
        {
            var dot = toolName.IndexOf('.');
            return dot < 0 ? null : toolName.Substring(0, dot);
        }

        ToolNamespace FindNamespace(string name)
        {
            foreach (var ns in _namespaces)
            {
                if (ns.Name == name)
                    return ns;
            }
            return null;
        }
    }
}
