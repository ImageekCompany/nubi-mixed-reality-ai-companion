using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// "What is this?" (architecture §5.3): SAM and the streamed vision answer run in parallel on the pre-captured frame.
    /// The answer is spoken as it streams; the mask is shown as soon as SAM (or its noun-phrase retry) returns.
    /// </summary>
    public sealed class CaptureAndIdentifyTool : ITool
    {
        const string Schema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"question\":{\"type\":\"string\",\"maxLength\":200,\"description\":\"The user's question about the object\"}," +
            "\"object_hint\":{\"type\":\"string\",\"maxLength\":40,\"description\":\"Optional short noun phrase if known, e.g. 'remote'\"}}," +
            "\"required\":[\"question\"],\"additionalProperties\":false}";

        readonly VisionToolkit _kit;
        readonly IObjectIdentifier _identifier;
        readonly ICharacterController _character;
        readonly ISceneMemory _scene;
        readonly ILocalizer _localizer;

        public CaptureAndIdentifyTool(VisionToolkit kit, IObjectIdentifier identifier, ICharacterController character,
                                      ISceneMemory scene, ILocalizer localizer)
        {
            _kit = kit;
            _identifier = identifier;
            _character = character;
            _scene = scene;
            _localizer = localizer;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.CaptureAndIdentify,
            "Look through the headset camera at what the user is showing or pointing at, highlight it, and answer the question about it.",
            Schema, TimeSpan.FromSeconds(12), ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("question", out var question);
            args.TryGetString("object_hint", out var hint);

            var image = _kit.Frames.IsFresh(context.Turn?.PreCaptured) ? context.Turn.PreCaptured : await _kit.TryCapture(ct);
            if (image == null)
                return ToolResult.Failure("camera_unavailable");
            _scene.LastCapture = image;

            var header = new UniTaskCompletionSource<string>();
            var identify = new VisionAnswerStream(_identifier, _kit.Clock, _kit.Settings.VisionTimeout)
                .Run(image, question, context, header, ct);
            var (answer, mask) = await UniTask.WhenAll(identify, Mask(image, hint, header.Task, ct));

            if (answer.TimedOut)
                return new ToolResult(false, ToolResult.Failure("vision_timeout").OutputJson,
                    CoreLines.Get(_localizer, CoreLines.HoldCloser), endsTurn: true);
            if (answer.Error != null)
                return ToolResult.Failure("vision_error", answer.Error.Kind.ToString());

            var label = answer.Label ?? hint ?? "object";
            string objectId = null;
            if (mask.HasValue)
                objectId = _scene.AddObject(label, mask.Value, "identified on camera").Id;
            var output = new ToolJson().Add("object_id", objectId).Add("label", label).Add("answer", answer.Text)
                                       .Add("masked", mask.HasValue);
            return ToolResult.Success(output.ToString(), endsTurn: true);
        }

        /// <summary>SAM with the planned prompt; one noun-phrase retry once the vision header names the object.</summary>
        async UniTask<Vector3?> Mask(EncodedImage image, string hint, UniTask<string> nounPhrase, CancellationToken ct)
        {
            var plan = _kit.Prompts.Plan(image, hint);
            var objects = await _kit.Segment(image, plan.Prompt, ct);
            if (_kit.Geometry.IsImplausible(objects, image, plan.HandPixels))
            {
                var phrase = await nounPhrase;
                if (!string.IsNullOrWhiteSpace(phrase))
                {
                    var retry = await _kit.Segment(image, SegmentPrompt.ForText(phrase), ct);
                    if (retry.Count > 0)
                        objects = retry;
                }
            }
            if (objects.Count == 0)
                return null;

            var best = MaskGeometry.Best(objects);
            var distance = _kit.Geometry.Locate(best, image, out var world);
            _kit.Present(new[] { best }, image, distance);
            _character.LookAt(LookTarget.At(world));
            return world;
        }
    }
}
