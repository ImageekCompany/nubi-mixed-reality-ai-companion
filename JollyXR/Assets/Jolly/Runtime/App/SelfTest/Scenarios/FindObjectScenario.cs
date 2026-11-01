using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// "Nubi, where is my mug? Show me." on the bundled still must run find_object: a fresh capture, a SAM text prompt for
    /// the noun phrase and every instance outlined. capture_and_identify is also a fair reading of the request, so another
    /// tool (or none) degrades with what the model called instead of failing.
    /// </summary>
    public static class FindObjectScenario
    {
        public const string Name = "vision_find_object";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var camera = c.Session.Camera;
            var masks = c.Session.Masks;
            camera.UseStill = true;
            try
            {
                var r = await VoiceTurnScenario.Run(c, Name, c.Assets.findObject, ToolNames.FindObject, ct,
                    otherToolDegrades: true);
                var toolAt = c.Observer.ToolStartedAt(ToolNames.FindObject);
                var shown = masks.ShownAt >= toolAt;
                var answerMs = c.Observer.AnswerAfter(ToolNames.FindObject) * 1000;
                r.Metric("frame", "bundled_still").Metric("found", shown ? masks.ShownCount : 0)
                 .Metric("tool_to_mask_ms", shown ? (masks.ShownAt - toolAt) * 1000 : double.NaN)
                 .Metric("tool_to_answer_audio_ms", answerMs);
                if (!c.Observer.ToolsCompleted.Contains(ToolNames.FindObject))
                    return r;
                r.Check(shown && masks.ShownCount > 0, "find_object outlined nothing on the still");
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
