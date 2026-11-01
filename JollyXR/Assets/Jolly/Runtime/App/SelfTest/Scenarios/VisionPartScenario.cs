using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// The second half of the demo's vision beat: right after <see cref="VisionScenario"/>, "Which part of the mug should I
    /// hold?" must run highlight_part on that turn's capture (a SAM text prompt, no new frame) and outline the part. The
    /// question is about the bundled still, so the turn runs on it; when the identify turn used the live camera, the still
    /// is captured first and stands in for it (frame=seeded_still).
    /// </summary>
    public static class VisionPartScenario
    {
        public const string Name = "vision_highlight_part";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var camera = c.Session.Camera;
            var scene = c.Graph.Conversation.Scene;
            var masks = c.Session.Masks;
            var seeded = scene.LastCapture == null || !camera.LastCaptureWasStill;
            camera.UseStill = true;
            try
            {
                if (seeded)
                    scene.LastCapture = await c.Graph.Conversation.Frames.Capture(ct);
                var capture = scene.LastCapture;
                var r = await VoiceTurnScenario.Run(c, Name, c.Assets.whichPart, ToolNames.HighlightPart, ct);
                var toolAt = c.Observer.ToolStartedAt(ToolNames.HighlightPart);
                var partShown = masks.ShownAt >= toolAt && capture != null && ReferenceEquals(masks.ShownSource, capture);
                var answerMs = c.Observer.AnswerAfter(ToolNames.HighlightPart) * 1000;
                r.Metric("frame", seeded ? "seeded_still" : "vision_still")
                 .Metric("tool_to_mask_ms", partShown ? (masks.ShownAt - toolAt) * 1000 : double.NaN)
                 .Metric("tool_to_answer_audio_ms", answerMs);
                // A missing or failed call has already failed the turn; the mask and answer checks would only repeat it.
                if (!c.Observer.ToolsCompleted.Contains(ToolNames.HighlightPart))
                    return r;
                r.Check(partShown, "no part mask on the identified capture");
                TurnChecks.ToolToAnswer(r, answerMs);
                return r;
            }
            finally
            {
                camera.UseStill = false;
            }
        }
    }
}
