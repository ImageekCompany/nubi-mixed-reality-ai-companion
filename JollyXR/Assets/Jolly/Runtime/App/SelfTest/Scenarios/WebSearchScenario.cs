using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// "What's the weather like today?" must run Meta's built-in web search (ADR-0048) and then speak an answer. The
    /// search's own latency (0.5–1 s) and the second model pass after it make its search → answer budget wider than a
    /// local tool's: past <see cref="SearchToAnswerBudgetMs"/> it degrades.
    /// </summary>
    public static class WebSearchScenario
    {
        public const string Name = "voice_web_search";
        /// <summary>Search start → answer audio was 1–6 s in the live probes (meta-model-api.md §10).</summary>
        public const double SearchToAnswerBudgetMs = 6000;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.webSearch, ToolNames.WebSearch, ct);
            var answerMs = c.Observer.AnswerAfter(ToolNames.WebSearch) * 1000;
            r.Metric("search_to_answer_audio_ms", answerMs);
            if (!c.Observer.ToolsCompleted.Contains(ToolNames.WebSearch))
                return r;
            if (r.Check(!double.IsNaN(answerMs), "no answer audio after the web search") && answerMs > SearchToAnswerBudgetMs)
                r.Degrade("web search → answer audio over the 6 s budget");
            return r;
        }
    }
}
