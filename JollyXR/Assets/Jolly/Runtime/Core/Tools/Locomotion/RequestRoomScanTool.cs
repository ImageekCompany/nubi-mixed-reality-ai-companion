using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// Opens the headset's Space Setup (ADR-0025). The request is queued until Nubi has finished speaking, because the app
    /// pauses as soon as Space Setup opens; the tool speaks its own short line and ends the turn.
    /// </summary>
    public sealed class RequestRoomScanTool : ITool
    {
        readonly IRoomScan _room;
        readonly ILocalizer _localizer;

        public RequestRoomScanTool(IRoomScan room, ILocalizer localizer)
        {
            _room = room;
            _localizer = localizer;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.RequestRoomScan,
            "Open the headset's Space Setup so the user can scan the room; afterwards Nubi can walk around the room and " +
            "to the furniture. Call it only after the user agrees to scan. The app pauses until they finish.",
            ToolSchemas.Empty, TimeSpan.FromSeconds(2), ToolConcurrency.Exclusive);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            var result = _room.RequestScan()
                ? ToolResult.Success("{\"status\":\"opening\"}", CoreLines.Get(_localizer, CoreLines.ScanOpening), endsTurn: true)
                : ToolResult.Failure("scan_unavailable");
            return UniTask.FromResult(result);
        }
    }
}
