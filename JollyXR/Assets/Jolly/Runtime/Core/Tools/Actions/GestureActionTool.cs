using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// A body action that plays clip gestures one after another (actions.jump, actions.bow, actions.celebrate;
    /// ADR-0047) and returns once the last has ended, so the model's next words come after the move. Logs
    /// <c>evt=action.&lt;verb&gt;</c>.
    /// </summary>
    public sealed class GestureActionTool : ITool
    {
        readonly ICharacterController _character;
        readonly ILog _log;
        readonly GestureId[] _sequence;

        public GestureActionTool(string name, string description, ICharacterController character, ILog log,
                                 params GestureId[] sequence)
        {
            _character = character;
            _log = log;
            _sequence = sequence;
            Definition = new ToolDefinition(name, description, ToolSchemas.Empty,
                TimeSpan.FromSeconds(4 * sequence.Length), ToolConcurrency.Exclusive);
        }

        public ToolDefinition Definition { get; }

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            foreach (var gesture in _sequence)
                await _character.PlayGesture(gesture, ct);
            _log.Write(LogLevel.Info, "tools", $"evt=action.{ActionVerb.Of(Definition.Name)} status=done");
            return ToolResult.Success("{\"status\":\"done\"}");
        }
    }

    static class ActionVerb
    {
        /// <summary>"jump" for "actions.jump".</summary>
        public static string Of(string toolName) => toolName.Substring(toolName.IndexOf('.') + 1);
    }
}
