using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.Contracts
{
    public enum ToolConcurrency
    {
        Parallel,     // runs alongside anything
        Locomotion,   // a newer Locomotion call supersedes the running one; returns once motion has started (see §4.2)
        Exclusive     // waits until the round's other non-Locomotion calls finish; never waits for Locomotion
    }

    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, string description, string parametersJsonSchema,
                              TimeSpan timeout, ToolConcurrency concurrency)
        { Name = name; Description = description; ParametersJsonSchema = parametersJsonSchema;
          Timeout = timeout; Concurrency = concurrency; }
        public string Name { get; }                  // ^[a-zA-Z0-9_.-]+$
        public string Description { get; }           // model-facing, English
        public string ParametersJsonSchema { get; }  // JSON Schema subset, see §4.1
        public TimeSpan Timeout { get; }
        public ToolConcurrency Concurrency { get; }
    }

    public interface IToolArguments
    {
        bool TryGetString(string name, out string value);
        bool TryGetNumber(string name, out double value);
        bool TryGetBool(string name, out bool value);
    }

    public readonly struct ToolResult
    {
        /// <summary>The output of a call a barge-in or other cancel cut off (Responses rejects an orphan function_call).</summary>
        public const string InterruptedOutput = "{\"error\":\"interrupted\"}";

        public ToolResult(bool ok, string outputJson, string spokenText = null, bool endsTurn = false, bool endsSpokenTurn = false)
        { Ok = ok; _outputJson = outputJson; SpokenText = spokenText; EndsTurn = endsTurn; EndsSpokenTurn = endsSpokenTurn; }
        public bool Ok { get; }
        /// <summary>Sent to the model as function_call_output. default(ToolResult) yields "{}".</summary>
        public string OutputJson => _outputJson ?? "{}";
        readonly string _outputJson;
        /// <summary>When set, the agent speaks it immediately. Null when the tool already streamed its speech via ToolContext.Speech.</summary>
        public string SpokenText { get; }
        /// <summary>If every result in a round has EndsTurn, the agent skips the next model round (saves ~1–1.5 s).</summary>
        public bool EndsTurn { get; }
        /// <summary>The call itself was the answer (a finished whole-body move, ADR-0047): if every result in a round has it,
        /// the round's text said something and the user's turn is one short clause with no connector, the agent skips the
        /// closing round. With no text the round still runs, so the turn is not left wordless; with a longer turn it runs
        /// for the rest of the request.</summary>
        public bool EndsSpokenTurn { get; }

        public static ToolResult Success(string outputJson, string spokenText = null, bool endsTurn = false, bool endsSpokenTurn = false)
            => new(true, outputJson, spokenText, endsTurn, endsSpokenTurn);
        /// <summary>code is snake_case, e.g. "unknown_place", "camera_unavailable", "timeout", "invalid_arguments".</summary>
        public static ToolResult Failure(string code, string detail = null)
            => new(false, "{\"error\":" + JsonString(code) + (detail == null ? "" : ",\"detail\":" + JsonString(detail)) + "}");

        /// <summary>RFC 8259 string escaping: quote, backslash, and every char below U+0020 (\n \r \t short forms, others \uXXXX).</summary>
        public static string JsonString(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length + 2).Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }

    /// <summary>Lets a tool stream speech into the turn's SpeechQueue (e.g. the vision answer) before it returns.</summary>
    public interface ISpokenTextSink
    {
        void Append(string delta);
    }

    public sealed class ToolContext
    {
        public UserTurn Turn { get; set; }
        public PersonaProfile Persona { get; set; }
        /// <summary>persona instructions + safety text + language directive, for tools that make their own model call.</summary>
        public string PersonaInstructions { get; set; }
        public ISpokenTextSink Speech { get; set; }
        public IUserPreferences Preferences { get; set; }
    }

    public interface ITool
    {
        ToolDefinition Definition { get; }
        /// <summary>Arguments are already validated against the schema. Must honour ct and must not throw for expected failures; return ToolResult.Failure.</summary>
        UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct);
    }

    /// <summary>
    /// A group of tools the model sees under one name and description (ADR-0047). A member tool is named
    /// "<c>namespace.function</c>": the model calls it by that name, and the request sends it inside the namespace as
    /// "function". Namespace names hold no dot.
    /// </summary>
    public sealed class ToolNamespace
    {
        public ToolNamespace(string name, string description) { Name = name; Description = description; }
        public string Name { get; }
        /// <summary>Model-facing, English: what the group is for, so the model knows when to reach into it.</summary>
        public string Description { get; }
    }

    public interface IToolRegistry
    {
        void Register(ITool tool);                                     // throws on a duplicate name
        /// <summary>Throws on a duplicate. A tool named "ns.fn" needs namespace ns registered first.</summary>
        void RegisterNamespace(ToolNamespace toolNamespace);
        /// <summary>The namespaces at least one of <paramref name="tools"/> belongs to, in registration order.</summary>
        IReadOnlyList<ToolNamespace> NamespacesOf(IReadOnlyList<ToolDefinition> tools);
        bool TryGet(string name, out ITool tool);
        /// <summary>Filtered by persona.AllowedTools; in seated mode walk_to, go_to_surface and follow_user are removed (come_here and actions.move_relative stay).</summary>
        IReadOnlyList<ToolDefinition> DefinitionsFor(PersonaProfile persona, IUserPreferences preferences);
        /// <summary>Validates against the tool's schema subset. On failure, error is a model-facing message.</summary>
        bool TryValidate(string name, string argumentsJson, out IToolArguments args, out string error);
    }
}
