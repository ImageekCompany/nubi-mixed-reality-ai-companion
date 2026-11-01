using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    public sealed class PlayGestureTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\"," +
            "\"enum\":[\"wave\",\"nod\",\"shake_head\",\"shrug\",\"point\",\"happy_bounce\",\"think\"]}}," +
            "\"required\":[\"name\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;

        public PlayGestureTool(ICharacterController character) => _character = character;

        public ToolDefinition Definition { get; } = new(ToolNames.PlayGesture, "Play an expressive body gesture.",
            Schema, TimeSpan.FromSeconds(4), ToolConcurrency.Parallel);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("name", out var name);
            await _character.PlayGesture(ToGesture(name), ct);
            return ToolResult.Success("{\"status\":\"played\"}");
        }

        /// <summary>The schema enum guarantees one of these names.</summary>
        static GestureId ToGesture(string name) => name switch
        {
            "wave" => GestureId.Wave,
            "nod" => GestureId.Nod,
            "shake_head" => GestureId.ShakeHead,
            "shrug" => GestureId.Shrug,
            "point" => GestureId.Point,
            "happy_bounce" => GestureId.HappyBounce,
            _ => GestureId.Think,
        };
    }
}
