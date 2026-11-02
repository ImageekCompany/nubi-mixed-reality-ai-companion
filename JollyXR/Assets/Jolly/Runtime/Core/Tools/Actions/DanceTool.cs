using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// actions.dance (ADR-0047): stands still first (<see cref="BodyAction"/>), loops the Dance clip until the first loop end at or after
    /// <c>duration_s</c>, then returns; the character keeps attending the user throughout (ADR-0041). Logs
    /// <c>evt=action.dance</c>.
    /// </summary>
    public sealed class DanceTool : ITool
    {
        const double DefaultSeconds = 4;
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"duration_s\":{\"type\":\"number\",\"minimum\":2,\"maximum\":12," +
            "\"description\":\"Roughly how long to dance, seconds; default 4\"}},\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly ILog _log;

        public DanceTool(ICharacterController character, ILog log)
        {
            _character = character;
            _log = log;
        }

        /// <summary>The longest dance, three 4 s loops, a margin and a walk to finish first.</summary>
        public ToolDefinition Definition { get; } = new(ToolNames.Actions.Dance,
            "Dance for the user, e.g. 'show me how to dance', 'dance for me', 'let's dance'.",
            Schema, TimeSpan.FromSeconds(18) + BodyAction.WalkWait, ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            var seconds = args.TryGetNumber("duration_s", out var d) ? d : DefaultSeconds;
            await BodyAction.Settle(_character, ct);
            await _character.PlayLooped(GestureId.Dance, (float)seconds, ct);
            var detail = $"duration_s={seconds.ToString("0.#", CultureInfo.InvariantCulture)} ";
            return BodyAction.CutShort(_character)
                ? BodyAction.Interrupted(_log, Definition.Name, detail)
                : BodyAction.Done(_log, Definition.Name, detail);
        }
    }
}
