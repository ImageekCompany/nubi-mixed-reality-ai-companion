using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// "What is this?" (architecture §5.3): SAM and the streamed vision answer run in parallel on the pre-captured frame.
    /// The answer is spoken as it streams; the mask is shown as soon as SAM (or its noun-phrase retry) returns. Asked
    /// where the thing goes, the answer can name a place the user showed earlier, and the tool then leads the way there
    /// and points (ADR-0051), so its timeout covers the walk and the vision part keeps its own.
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
        static readonly TimeSpan VisionBudget = TimeSpan.FromSeconds(12);

        readonly ICharacterController _character;
        readonly ISceneMemory _scene;
        readonly ILocalizer _localizer;
        readonly PointToPlace _pointer;

        public CaptureAndIdentifyTool(VisionToolkit kit, IObjectIdentifier identifier, ICharacterController character,
                                      ISceneMemory scene, ILocalizer localizer, PointToPlace pointer)
        {
            _pointer = pointer;
            _kit = kit;
            _identifier = identifier;
            _character = character;
            _scene = scene;
            _localizer = localizer;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.CaptureAndIdentify,
            "Look through the headset camera at what the user is showing or pointing at, highlight it, " +
            "and answer the question about it. Use it whenever the user asks what something is, how to use it, " +
            "where to put or store what they are showing, or says 'look at this'. " +
            "If they ask where it goes and a listed place fits, you walk there and point by yourself.",
            Schema, VisionBudget + PointToPlace.WalkBudget, ToolConcurrency.Exclusive);

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("question", out var question);
            args.TryGetString("object_hint", out var hint);

            using var budget = new TimeoutScope(_kit.Clock, VisionBudget, ct);
            VisionAnswer answer;
            Vector3? mask;
            try
            {
                var outcome = await Identify(question, hint, context, budget.Token);
                if (outcome.Failure.HasValue)
                    return outcome.Failure.Value;
                (answer, mask) = (outcome.Answer, outcome.Mask);
            }
            catch (OperationCanceledException) when (budget.TimedOut && !ct.IsCancellationRequested)
            {
                return ToolResult.Failure("timeout");
            }

            var label = answer.Label ?? hint ?? "object";
            var objectId = mask.HasValue ? _scene.AddObject(label, mask.Value, "identified on camera").Id : null;
            var output = new ToolJson().Add("object_id", objectId).Add("label", label).Add("answer", answer.Text)
                                       .Add("masked", mask.HasValue);

            if (answer.Place == null || !_scene.TryGetPlace(answer.Place, out var place))
                return ToolResult.Success(output.ToString(), endsTurn: true);
            output.Add("place", place.Name);
            var pointed = await _pointer.Run(place.Pose.position, ct);
            return new ToolResult(true, output.Add("shown", pointed).ToString(),
                pointed ? CoreLines.Get(_localizer, CoreLines.PlaceThere) : null, endsTurn: true);
        }

        readonly struct Identified
        {
            public Identified(ToolResult failure) { Failure = failure; Answer = default; Mask = null; }
            public Identified(in VisionAnswer answer, Vector3? mask) { Failure = null; Answer = answer; Mask = mask; }
            public ToolResult? Failure { get; }
            public VisionAnswer Answer { get; }
            public Vector3? Mask { get; }
        }

        async UniTask<Identified> Identify(string question, string hint, ToolContext context, CancellationToken ct)
        {
            var started = _kit.Clock.Now;
            var preCaptured = _kit.Frames.IsFresh(context.Turn?.PreCaptured);
            var image = preCaptured ? context.Turn.PreCaptured : await _kit.TryCapture(ct);
            if (image == null)
                return new Identified(ToolResult.Failure("camera_unavailable"));
            if (_kit.IsTooDark(image))
                return new Identified(VisionToolkit.TooDark);
            _scene.LastCapture = image;

            var header = new UniTaskCompletionSource<string>();
            var identify = new VisionAnswerStream(_identifier, _kit).Run(image, question, context, header, ct, PlaceNames());
            var maskShownAt = double.NaN;
            var (answer, mask) = await UniTask.WhenAll(identify, Mask(image, hint, header.Task, at => maskShownAt = at, ct));
            LogTiming(started, preCaptured, answer, maskShownAt);

            if (answer.TimedOut)
                return new Identified(new ToolResult(false, ToolResult.Failure("vision_timeout").OutputJson,
                    CoreLines.Get(_localizer, CoreLines.HoldCloser), endsTurn: true));
            if (answer.Error != null)
                return new Identified(ToolResult.Failure("vision_error", answer.Error.Kind.ToString()));
            // Seen on the headset with a dim live frame: the stream completed with nothing to say, and the turn ended
            // silently right after the preamble.
            if (string.IsNullOrWhiteSpace(answer.Text))
                return new Identified(new ToolResult(false, ToolResult.Failure("vision_empty").OutputJson,
                    CoreLines.Get(_localizer, CoreLines.HoldCloser), endsTurn: true));
            return new Identified(answer, mask);
        }

        string[] PlaceNames()
        {
            var places = _scene.Places;
            if (places.Count == 0)
                return null;
            var names = new string[places.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = places[i].Name;
            return names;
        }

        /// <summary>SAM with the planned prompt; one noun-phrase retry once the vision header names the object.</summary>
        async UniTask<Vector3?> Mask(EncodedImage image, string hint, UniTask<string> nounPhrase, Action<double> shown,
                                     CancellationToken ct)
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
            shown(_kit.Clock.Now);
            _character.LookAt(LookTarget.At(world));
            return world;
        }

        /// <summary>Milliseconds from the tool start (DoD §5: mask ≤ 2 s; answer audio ≤ 2.5 s, which adds TTS).</summary>
        void LogTiming(double started, bool preCaptured, in VisionAnswer answer, double maskShownAt)
        {
            if (!_kit.Log.IsEnabled(LogLevel.Info))
                return;
            _kit.Log.Write(LogLevel.Info, "vision", string.Format(CultureInfo.InvariantCulture,
                "evt=vision.timing frame={0} header_ms={1:F0} answer_text_ms={2:F0} mask_ms={3:F0}",
                preCaptured ? "pre" : "fresh", (answer.HeaderAt - started) * 1000, (answer.FirstTextAt - started) * 1000,
                (maskShownAt - started) * 1000));
        }
    }
}
