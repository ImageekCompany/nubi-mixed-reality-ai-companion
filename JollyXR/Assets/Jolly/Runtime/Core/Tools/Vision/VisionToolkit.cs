using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// The capture → SAM → world → overlay steps shared by capture_and_identify, find_object and highlight_part
    /// (architecture §5.3, §5.3.1).
    /// </summary>
    public sealed class VisionToolkit
    {
        static readonly IReadOnlyList<SegmentedObject> NoObjects = Array.Empty<SegmentedObject>();

        readonly ISegmenter _segmenter;
        readonly IMaskPresenter _presenter;
        readonly ILog _log;

        public VisionToolkit(FrameGrabber frames, SegmentPromptPlanner prompts, MaskGeometry geometry, ISegmenter segmenter,
                             IMaskPresenter presenter, IClock clock, ILog log, VisionSettings settings)
        {
            Frames = frames;
            Prompts = prompts;
            Geometry = geometry;
            Settings = settings;
            Clock = clock;
            _segmenter = segmenter;
            _presenter = presenter;
            _log = log;
        }

        public FrameGrabber Frames { get; }
        public SegmentPromptPlanner Prompts { get; }
        public MaskGeometry Geometry { get; }
        public VisionSettings Settings { get; }
        public IClock Clock { get; }

        /// <summary>A fresh capture, or null when the camera is unavailable or delivers no frame.</summary>
        public async UniTask<EncodedImage> TryCapture(CancellationToken ct)
        {
            try
            {
                return await Frames.Capture(ct);
            }
            catch (ProviderException e)
            {
                _log.Write(LogLevel.Warning, "vision", $"evt=capture.failed kind={e.Kind}");
                return null;
            }
        }

        /// <summary>SAM with its own timeout. A failure or timeout reads as "no match"; only the caller's cancel throws.</summary>
        public async UniTask<IReadOnlyList<SegmentedObject>> Segment(EncodedImage image, SegmentPrompt prompt, CancellationToken ct)
        {
            using var scope = new TimeoutScope(Clock, Settings.SamTimeout, ct);
            try
            {
                return await _segmenter.Segment(image, prompt, scope.Token);
            }
            catch (OperationCanceledException) when (scope.TimedOut && !ct.IsCancellationRequested)
            {
                _log.Write(LogLevel.Warning, "vision", "evt=sam.timeout");
            }
            catch (ProviderException e)
            {
                _log.Write(LogLevel.Warning, "vision", $"evt=sam.failed kind={e.Kind} status={e.HttpStatus}");
            }
            return NoObjects;
        }

        public void Present(IReadOnlyList<SegmentedObject> objects, EncodedImage image, float distance) =>
            _presenter.Show(objects, image, distance);
    }
}
