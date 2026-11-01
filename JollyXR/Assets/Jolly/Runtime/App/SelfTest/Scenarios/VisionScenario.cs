using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// "What is this and how do I use it?" must run capture_and_identify on the camera frame (SAM mask + Spark answer).
    /// A frame too dark to show anything (or no camera) switches to the bundled still and marks the result DEGRADED.
    /// </summary>
    public static class VisionScenario
    {
        public const string Name = "vision_capture_and_identify";
        const float MinMeanLuma = 25f;
        const int ProbeSide = 160;
        // The preamble ("Let me check.") plays within ~0.8 s of the tool start; the answer cannot: its header alone took
        // ≥ 1.5 s in every measured call (docs/perf/selftest-report.md). So the first caption after this is the answer.
        const double AnswerNotBeforeSeconds = 1.0;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var camera = c.Session.Camera;
            var luma = await MeanLuma(camera.Live, ct);
            camera.UseStill = double.IsNaN(luma) || luma < MinMeanLuma;
            try
            {
                var r = await VoiceTurnScenario.Run(c, Name, c.Assets.whatIsThis, ToolNames.CaptureAndIdentify, ct);
                var toolToAnswer = ToolToAnswerAudioMs(c.Observer);
                r.Metric("camera", camera.Live.Availability.ToString()).Metric("mean_luma", luma)
                 .Metric("frame", camera.UseStill ? "bundled_still" : "live")
                 .Metric("tool_to_answer_audio_ms", toolToAnswer);
                TurnChecks.ToolToAnswer(r, toolToAnswer);
                if (camera.UseStill)
                    r.Degrade("camera frame unusable (dark room or no camera): identified the bundled still instead");
                return r;
            }
            finally
            {
                camera.UseStill = false;
            }
        }

        /// <summary>DoD §5 "tool start → answer audio"; NaN when no caption followed the tool.</summary>
        static double ToolToAnswerAudioMs(TurnObserver observer)
        {
            var tool = observer.FirstToolStartedAt;
            foreach (var at in observer.CaptionTimes)
            {
                if (at - tool >= AnswerNotBeforeSeconds)
                    return (at - tool) * 1000;
            }
            return double.NaN;
        }

        /// <summary>NaN when no frame could be captured.</summary>
        static async UniTask<double> MeanLuma(ICameraFrameSource source, CancellationToken ct)
        {
            if (source.Availability != CameraAvailability.Ready)
                return double.NaN;
            CameraFrame frame;
            try
            {
                frame = await source.Capture(ProbeSide, ct);
            }
            catch (ProviderException)
            {
                return double.NaN;
            }
            using (frame)
            {
                var pixels = frame.Rgba32;
                double sum = 0;
                var count = 0;
                for (var i = 0; i + 2 < pixels.Length; i += 4)
                {
                    sum += 0.299 * pixels[i] + 0.587 * pixels[i + 1] + 0.114 * pixels[i + 2];
                    count++;
                }
                return count == 0 ? double.NaN : sum / count;
            }
        }
    }
}
