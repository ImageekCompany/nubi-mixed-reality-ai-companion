using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// The capture → SAM → world → overlay steps shared by capture_and_identify, find_object and highlight_part
    /// (architecture §5.3, §5.3.1). Each SAM and Spark-vision call that answers raises ModelCallCompleted, so the
    /// badges name the models the vision path used.
    /// </summary>
    public sealed class VisionToolkit
    {
        static readonly IReadOnlyList<SegmentedObject> NoObjects = Array.Empty<SegmentedObject>();

        /// <summary>Spark on a dim live frame once streamed no answer at all (selftest-report); a frame this dark gets no
        /// SAM or Spark call and the model asks for light in one sentence (PromptBuilder tool rules, ADR-0039).</summary>
        public static readonly ToolResult TooDark = ToolResult.Failure("too_dark",
            "the room is too dark to see; ask them to switch on a light or hold it closer to one");

        readonly ISegmenter _segmenter;
        readonly IMaskPresenter _presenter;
        readonly ConversationEvents _events;

        public VisionToolkit(FrameGrabber frames, SegmentPromptPlanner prompts, MaskGeometry geometry, ISegmenter segmenter,
                             IMaskPresenter presenter, IClock clock, ILog log, VisionSettings settings,
                             ConversationEvents events)
        {
            Frames = frames;
            Prompts = prompts;
            Geometry = geometry;
            Settings = settings;
            Clock = clock;
            Log = log;
            _segmenter = segmenter;
            _presenter = presenter;
            _events = events;
        }

        public FrameGrabber Frames { get; }
        public SegmentPromptPlanner Prompts { get; }
        public MaskGeometry Geometry { get; }
        public VisionSettings Settings { get; }
        public IClock Clock { get; }
        public ILog Log { get; }

        /// <summary>A fresh capture, or null when the camera is unavailable or delivers no frame.</summary>
        public async UniTask<EncodedImage> TryCapture(CancellationToken ct)
        {
            try
            {
                return await Frames.Capture(ct);
            }
            catch (ProviderException e)
            {
                Log.Write(LogLevel.Warning, "vision", $"evt=capture.failed kind={e.Kind}");
                return null;
            }
        }

        /// <summary>True, and logged, when <paramref name="image"/> is too dark to send to SAM or Spark.</summary>
        public bool IsTooDark(EncodedImage image)
        {
            var luma = Frames.MeanLumaOf(image);
            if (!Frames.IsTooDark(luma))
                return false;
            Log.Write(LogLevel.Info, "vision", string.Format(CultureInfo.InvariantCulture,
                "evt=vision.too_dark luma={0:F1} min={1:F1}", luma, Settings.MinMeanLuma));
            return true;
        }

        /// <summary>SAM with its own timeout. A failure or timeout reads as "no match"; only the caller's cancel throws.</summary>
        public async UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct)
        {
            using var scope = new TimeoutScope(Clock, Settings.SamTimeout, ct);
            var started = Clock.Now;
            try
            {
                var objects = await _segmenter.Segment(image, prompt, scope.Token);
                ReportModelCall(Settings.SegmenterModel, started);
                return objects;
            }
            catch (OperationCanceledException) when (scope.TimedOut && !ct.IsCancellationRequested)
            {
                Log.Write(LogLevel.Warning, "vision", "evt=sam.timeout");
            }
            catch (ProviderException e)
            {
                Log.Write(LogLevel.Warning, "vision", $"evt=sam.failed kind={e.Kind} status={e.HttpStatus}");
            }
            return NoObjects;
        }

        public void Present(IReadOnlyList<SegmentedObject> objects, EncodedImage image, float distance) =>
            _presenter.Show(objects, image, distance);

        /// <summary>A model call that answered, timed from <paramref name="startedAt"/> (an <see cref="IClock.Now"/> value).</summary>
        public void ReportModelCall(string model, double startedAt) =>
            _events.RaiseModelCallCompleted(model, (Clock.Now - startedAt) * 1000.0);
    }
}
