using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// One full spoken turn through the real Muse ASR → Spark → Wit TTS → output + lip-sync, from a bundled clip.
    /// Checks the transcript, the reply, the first-audio budget and the tools (<see cref="TurnChecks"/>); with
    /// <c>otherToolDegrades</c> another tool than <c>expectedTool</c> degrades instead of failing (<see cref="TurnChecks.PreferredTool"/>).
    /// With <c>checkFacing</c> the character must face the user at first audio (<see cref="TurnChecks.Facing"/>); the
    /// sample is recorded either way.
    /// </summary>
    public static class VoiceTurnScenario
    {
        const double MinSimilarity = 0.5;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, string name, SelfTestAssets.Utterance utterance,
                                                        string expectedTool, CancellationToken ct,
                                                        bool otherToolDegrades = false, bool checkFacing = true)
        {
            var r = new ScenarioResult(name);
            if (!r.Check(utterance.clip != null, "no clip bundled"))
                return r;
            await c.WaitForIdle(20, ct);
            c.Observer.Reset();
            var look = c.Graph.Look;
            var looksBefore = look.Last.Turn;
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
             .Metric("llm_reply", string.Join(" | ", c.Observer.Replies))
             .Metric("states", c.Observer.StatePath());

            r.Check(idle, "did not return to Idle within 40 s");
            r.Check(similarity >= MinSimilarity, "transcript too far from the spoken text");
            r.Check(c.Observer.Captions.Count > 0, "no reply caption");
            TurnChecks.FirstAudio(r, firstAudio);
            TurnChecks.Facing(r, look.Last.Turn != looksBefore, look.Last, checkFacing);
            if (otherToolDegrades)
                TurnChecks.PreferredTool(r, c.Observer.ToolsStarted, c.Observer.ToolsCompleted, expectedTool);
            else
                TurnChecks.Tools(r, c.Observer.ToolsStarted, c.Observer.ToolsCompleted, expectedTool);
            return r;
        }
    }
}
