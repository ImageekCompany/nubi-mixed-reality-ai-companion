using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>Starts follow mode, which persists until Stop() or another locomotion command.</summary>
    public sealed class FollowUserTool : ITool
    {
        readonly ICharacterController _character;
        readonly LocomotionSettings _settings;

        public FollowUserTool(ICharacterController character, LocomotionSettings settings)
        {
            _character = character;
            _settings = settings;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.FollowUser,
            "Start following the user around at a comfortable distance until told to stop or sent elsewhere; " +
            "only when the user asks you to follow them.",
            ToolSchemas.Empty, TimeSpan.FromSeconds(1), ToolConcurrency.Locomotion);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            _character.Follow(_settings.FollowMinDistance, _settings.FollowMaxDistance);
            return UniTask.FromResult(ToolResult.Success("{\"status\":\"following\"}"));
        }
    }
}
