using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// actions.spin (ADR-0047): stands still first (<see cref="BodyAction"/>) and turns on the spot with a happy bounce, then faces the user again. Procedural, so every
    /// character can do it. Logs <c>evt=action.spin</c>.
    /// </summary>
    public sealed class SpinTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"turns\":{\"type\":\"number\",\"minimum\":1,\"maximum\":3,\"description\":\"Full turns; default 1\"}," +
            "\"direction\":{\"type\":\"string\",\"enum\":[\"left\",\"right\"],\"description\":\"Which way to turn; default right\"}}," +
            "\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly ILog _log;

        public SpinTool(ICharacterController character, ILog log)
        {
            _character = character;
            _log = log;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.Actions.Spin,
            "Spin around on the spot, e.g. 'spin around', 'do a twirl', 'turn around'.",
            Schema, TimeSpan.FromSeconds(6) + BodyAction.WalkWait, ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            var turns = args.TryGetNumber("turns", out var t) ? Mathf.RoundToInt((float)t) : 1;
            var left = args.TryGetString("direction", out var direction) && direction == "left";
            // Positive is clockwise seen from above: a turn to the right.
            var degrees = turns * 360f * (left ? -1f : 1f);
            await BodyAction.Settle(_character, ct);
            await UniTask.WhenAll(_character.Spin(degrees, ct), _character.PlayGesture(GestureId.HappyBounce, ct));
            var detail = $"turns={turns} dir={(left ? "left" : "right")} ";
            return BodyAction.CutShort(_character)
                ? BodyAction.Interrupted(_log, Definition.Name, detail)
                : BodyAction.Done(_log, Definition.Name, detail);
        }
    }
}
