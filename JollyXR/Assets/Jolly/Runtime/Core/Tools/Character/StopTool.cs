using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>Halts locomotion and ends the turn. Usually pre-empted by the local StopGuard before any model call.</summary>
    public sealed class StopTool : ITool
    {
        readonly ICharacterController _character;

        public StopTool(ICharacterController character) => _character = character;

        public ToolDefinition Definition { get; } = new(ToolNames.Stop, "Stop walking, following and talking immediately.",
            ToolSchemas.Empty, TimeSpan.FromSeconds(1), ToolConcurrency.Parallel);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            _character.Stop();
            return UniTask.FromResult(ToolResult.Success("{\"status\":\"stopped\"}", endsTurn: true));
        }
    }
}
