using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// One full spoken turn through the real Muse ASR → Spark → Wit TTS → output + lip-sync, from a bundled clip.
    /// Checks the transcript, the reply and, when given, the tool the model should call.
    /// </summary>
    public static class VoiceTurnScenario
    {
        const double MinSimilarity = 0.5;
        const double BudgetMs = 2000;
        const double MaxFirstAudioMs = 4000;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, string name, SelfTestAssets.Utterance utterance,
                                                        string expectedTool, CancellationToken ct)
        {
            var r = new ScenarioResult(name);
            if (!r.Check(utterance.clip != null, "no clip bundled"))
                return r;
            await c.WaitForIdle(20, ct);
            c.Observer.Reset();
            await c.SpeakClip(utterance.clip, ct);
            var idle = await c.WaitForIdle(40, ct);

            var latency = c.Graph.Foundation.Latency;
            var firstAudio = latency.Between(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed);
            var similarity = TextSimilarity.Words(c.Observer.UserFinal, utterance.expectedText);
            r.Metric("transcript", c.Observer.UserFinal).Metric("similarity", similarity)
             .Metric("release_to_first_audio_ms", firstAudio)
             .Metric("release_to_asr_final_ms", latency.Between(LatencyMark.PttReleased, LatencyMark.AsrFinal))
             .Metric("asr_final_to_llm_text_ms", latency.Between(LatencyMark.AsrFinal, LatencyMark.LlmFirstText))
             .Metric("reply", string.Join(" ", c.Observer.Captions))
             .Metric("tools", string.Join(",", c.Observer.ToolsCompleted))
             .Metric("states", c.Observer.StatePath());

            r.Check(idle, "did not return to Idle within 40 s");
            r.Check(similarity >= MinSimilarity, "transcript too far from the spoken text");
            r.Check(c.Observer.Captions.Count > 0, "no reply caption");
            r.Check(firstAudio <= MaxFirstAudioMs, "no reply audio within 4 s of release");
            if (firstAudio > BudgetMs)
                r.Notes.Add("first audio over the 2.0 s budget");
            if (expectedTool != null)
                CheckTool(c, r, expectedTool);
            return r;
        }

        static void CheckTool(SelfTestContext c, ScenarioResult r, string tool)
        {
            r.Check(c.Observer.ToolsStarted.Contains(tool), "the model did not call " + tool);
            r.Check(c.Observer.ToolsCompleted.Contains(tool) || c.Observer.ToolsCompleted.Contains(tool + ":failed"),
                tool + " did not complete");
        }
    }
}
