using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Diagnostics;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// <c>selftest.sh --turns N</c>: N consecutive online voice turns in one session, rotating the greeting, come-here and
    /// vision scenarios, for the release → first audio distribution behind the DoD §5 p50 claim; the default run has only
    /// four such turns, too few to tell a p50 of 1.8 s from 2.1 s. Each turn keeps its own scenario checks; a failed turn
    /// fails the whole run. One turn in three is a vision turn: the demo's mix, without a SAM call on every turn.
    /// </summary>
    public static class LatencyTurnsScenario
    {
        public const string Name = "latency_turns";

        delegate UniTask<ScenarioResult> Turn(SelfTestContext c, CancellationToken ct);

        static readonly Turn[] Rotation = { GreetingScenario.Run, ComeHereScenario.Run, VisionScenario.Run };

        /// <summary>The breakdown of selftest-report.md: release → first audio, then the stages that add up to it.</summary>
        static readonly (string Name, LatencyMark From, LatencyMark To)[] Stages =
        {
            ("release_to_first_audio", LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed),
            ("release_to_asr_final", LatencyMark.PttReleased, LatencyMark.AsrFinal),
            ("asr_final_to_request", LatencyMark.AsrFinal, LatencyMark.LlmRequestSent),
            ("request_to_first_event", LatencyMark.LlmRequestSent, LatencyMark.LlmFirstEvent),
            ("first_event_to_first_text", LatencyMark.LlmFirstEvent, LatencyMark.LlmFirstText),
            ("first_text_to_sentence", LatencyMark.LlmFirstText, LatencyMark.FirstSentenceQueued),
            ("sentence_to_first_audio", LatencyMark.FirstSentenceQueued, LatencyMark.FirstAudioPlayed),
        };

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, int turns, CancellationToken ct)
        {
            var r = new ScenarioResult(Name);
            var series = new LatencySeries[Stages.Length];
            for (var s = 0; s < series.Length; s++)
                series[s] = new LatencySeries();
            var latency = c.Graph.Foundation.Latency;
            var byTurn = new List<string>(turns);
            var degraded = 0;
            for (var i = 1; i <= turns; i++)
            {
                var turn = await Rotation[(i - 1) % Rotation.Length](c, ct);
                // Not back in Idle: the turn never completed, and the tracker still holds the previous turn's marks.
                var completed = c.State == ConversationState.Idle;
                var firstAudio = completed ? latency.Between(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed) : double.NaN;
                if (completed)
                {
                    for (var s = 0; s < Stages.Length; s++)
                        series[s].Add(latency.Between(Stages[s].From, Stages[s].To));
                }
                byTurn.Add(double.IsNaN(firstAudio) ? "null" : firstAudio.ToString("0", CultureInfo.InvariantCulture));
                var notes = string.Join("; ", turn.Notes);
                c.Log.Write(LogLevel.Info, "selftest", $"evt=selftest.turn i={i} name={turn.Name} status={turn.Status} " +
                                                       $"first_audio_ms={firstAudio:0} notes=\"{notes}\"");
                if (turn.Status == ScenarioStatus.Degraded)
                    degraded++;
                else
                    r.Check(turn.Status != ScenarioStatus.Fail, $"turn {i} ({turn.Name}): {notes}");
            }

            r.Metric("turns", turns).Metric("turns_degraded", degraded)
             .Metric("first_audio_ms_by_turn", string.Join(",", byTurn));
            for (var s = 0; s < Stages.Length; s++)
            {
                var (name, _, _) = Stages[s];
                var stage = series[s];
                r.Metric(name + "_n", stage.Count).Metric(name + "_p50_ms", stage.Percentile(50))
                 .Metric(name + "_p90_ms", stage.Percentile(90)).Metric(name + "_max_ms", stage.Max);
                c.Log.Write(LogLevel.Info, "selftest", $"evt=selftest.latency stage={name} {stage.Summary()}");
            }
            if (series[0].Percentile(50) > TurnChecks.FirstAudioBudgetMs)
                r.Degrade("release → first audio p50 over the 2.0 s budget");
            return r;
        }
    }
}
