using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Composition;
using Jolly.App.SelfTest.Scenarios;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// Runs the scenarios in order (cheap and offline first, paid ones after, perf last), logs one
    /// <c>evt=selftest.result</c> line each and writes the results file. A scenario that throws fails alone. With
    /// <see cref="SelfTestOptions.LatencyTurns"/> the list is <see cref="LatencyTurnsScenario"/> and then perf.
    /// </summary>
    public sealed class SelfTestRunner
    {
        delegate UniTask<ScenarioResult> Scenario(SelfTestContext context, CancellationToken ct);

        static readonly (string Name, Scenario Run)[] Scenarios =
        {
            (OfflineGreetingScenario.Name, OfflineGreetingScenario.Run),
            (GreetingScenario.Name, GreetingScenario.Run),
            (ComeHereScenario.Name, ComeHereScenario.Run),
            // Free and local; after come_here, which has settled the room and stands the character in front of the user.
            (TapToMoveScenario.Name, TapToMoveScenario.Run),
            (VisionScenario.Name, VisionScenario.Run),
            // Asks about a part of what the identify turn just saw, so it must follow it.
            (VisionPartScenario.Name, VisionPartScenario.Run),
            (FindObjectScenario.Name, FindObjectScenario.Run),
            (WebSearchScenario.Name, WebSearchScenario.Run),
            (ActionDanceScenario.Name, ActionDanceScenario.Run),
            // Leaves the character behind the user, so it runs after every scenario that wants it in front.
            (ActionMoveBehindScenario.Name, ActionMoveBehindScenario.Run),
            (BargeInScenario.Name, BargeInScenario.Run),
            (PerfScenario.Name, PerfScenario.Run),
        };

        readonly SelfTestSession _session;
        readonly SelfTestAssets _assets;
        readonly AppGraph _graph;
        readonly string _resultsPath;

        public SelfTestRunner(SelfTestSession session, SelfTestAssets assets, AppGraph graph, string resultsPath)
        {
            _session = session;
            _assets = assets;
            _graph = graph;
            _resultsPath = resultsPath;
        }

        public async UniTaskVoid Run(CancellationToken ct)
        {
            var f = _graph.Foundation;
            var started = DateTimeOffset.UtcNow.ToString("O");
            var spentBefore = f.Usage.SpentUsd;
            var scenarios = ScenariosFor(_session.Options);
            var results = new List<ScenarioResult>(scenarios.Length);
            using var observer = new TurnObserver(f.Events, f.Clock);
            var context = new SelfTestContext(_session, _assets, _graph, observer);
            _session.ModelReplied += observer.RecordReply;
            try
            {
                await _graph.WhenRunning.AttachExternalCancellation(ct);
                var connectivity = _graph.Cloud.Connectivity;
                var initial = await connectivity.ProbeNow(ct);
                f.Log.Write(LogLevel.Info, "selftest",
                    $"evt=selftest.network reachable={_session.NetworkReachable} probe={initial}");
                await context.WaitUntil(() => connectivity.State == Connectivity.Online, 20, ct);
                LogFrames("launch");
                foreach (var (name, run) in scenarios)
                {
                    // One capture per turn scenario (the Profiler keeps 2,000 frames); perf_idle runs without its overhead.
                    if (name != PerfScenario.Name)
                        _session.Profile?.Begin(name);
                    results.Add(await RunOne(name, run, context, ct));
                    _session.Profile?.End();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            finally
            {
                _session.ModelReplied -= observer.RecordReply;
                _session.Profile?.End();
            }

            _session.SaveGraphicsStates(f.Log);
            var overall = Overall(results);
            ResultsWriter.Write(_resultsPath, SystemInfo.deviceModel, started, f.Usage.SpentUsd - spentBefore, overall, results);
            f.Log.Write(LogLevel.Info, "selftest",
                $"evt=selftest.done overall={overall} spent_usd={f.Usage.SpentUsd - spentBefore:0.0000}");
        }

        static (string Name, Scenario Run)[] ScenariosFor(SelfTestOptions options)
        {
            var turns = options.LatencyTurns;
            if (turns == 0)
                return Scenarios;
            return new (string, Scenario)[]
            {
                (LatencyTurnsScenario.Name, (c, ct) => LatencyTurnsScenario.Run(c, turns, ct)),
                (PerfScenario.Name, PerfScenario.Run),
            };
        }

        async UniTask<ScenarioResult> RunOne(string name, Scenario run, SelfTestContext context, CancellationToken ct)
        {
            var log = _graph.Foundation.Log;
            log.Write(LogLevel.Info, "selftest", "evt=selftest.start name=" + name);
            var startedAt = context.Clock.Now;
            ScenarioResult result;
            try
            {
                result = await run(context, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                result = new ScenarioResult(name);
                result.Check(false, "threw " + e.GetType().Name + ": " + e.Message);
                log.Write(LogLevel.Error, "selftest", "evt=selftest.crashed name=" + name, e);
            }
            result.DurationMs = (context.Clock.Now - startedAt) * 1000;
            log.Write(LogLevel.Info, "selftest", $"evt=selftest.result name={name} status={result.Status} " +
                                                 $"ms={result.DurationMs:0} notes=\"{string.Join("; ", result.Notes)}\"");
            LogFrames(name);
            return result;
        }

        void LogFrames(string phase) =>
            _graph.Foundation.Log.Write(LogLevel.Info, "selftest", $"evt=selftest.frames name={phase} {_session.Frames.TakeWindow()}");

        static string Overall(List<ScenarioResult> results)
        {
            var degraded = false;
            foreach (var r in results)
            {
                if (r.Status == ScenarioStatus.Fail)
                    return "FAIL";
                degraded |= r.Status == ScenarioStatus.Degraded;
            }
            return degraded ? "DEGRADED" : "PASS";
        }
    }
}
