using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>"Which part do I hold?" (architecture §5.3.1): SAM text prompt for a part phrase on the last capture
    /// (RAM only), outlined in place; Nubi looks at it.</summary>
    public sealed class HighlightPartTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{\"object_id\":{\"type\":\"string\",\"maxLength\":16}," +
            "\"part\":{\"type\":\"string\",\"maxLength\":40,\"description\":\"part noun phrase, e.g. 'mug handle'\"}}," +
            "\"required\":[\"part\"],\"additionalProperties\":false}";

        readonly VisionToolkit _kit;
        readonly ICharacterController _character;
        readonly ISceneMemory _scene;

        public HighlightPartTool(VisionToolkit kit, ICharacterController character, ISceneMemory scene)
        {
            _kit = kit;
            _character = character;
            _scene = scene;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.HighlightPart,
            "Highlight a part of an object seen this session, e.g. 'handle' of obj_3. " +
            "Use it when the user asks which part or button to use.",
            Schema, TimeSpan.FromSeconds(6), ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("part", out var part);
            var image = _scene.LastCapture;
            if (image == null)
                return ToolResult.Failure("no_frame");

            var objects = await _kit.Segment(image, SegmentPrompt.ForText(part), ct);
            if (objects.Count == 0)
                return ToolResult.Failure("no_match");

            var best = MaskGeometry.Best(objects);
            var distance = _kit.Geometry.Locate(best, image, out var world);
            _kit.Present(new[] { best }, image, distance);
            _character.LookAt(LookTarget.At(world));
            return ToolResult.Success(new ToolJson().Add("status", "highlighted").Add("part", part).ToString());
        }
    }
}
