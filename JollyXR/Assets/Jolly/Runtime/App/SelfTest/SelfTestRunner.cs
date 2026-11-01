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
    /// <c>evt=selftest.result</c> line each and writes the results file. A scenario that throws fails alone.
    /// </summary>
    public sealed class SelfTestRunner
    {
        delegate UniTask<ScenarioResult> Scenario(SelfTestContext context, CancellationToken ct);

        static readonly (string Name, Scenario Run)[] Scenarios =
        {
            (OfflineGreetingScenario.Name, OfflineGreetingScenario.Run),
            ("voice_greeting", (c, ct) => VoiceTurnScenario.Run(c, "voice_greeting", c.Assets.greeting, null, ct)),
            (ComeHereScenario.Name, ComeHereScenario.Run),
            (VisionScenario.Name, VisionScenario.Run),
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
            var results = new List<ScenarioResult>(Scenarios.Length);
            using var observer = new TurnObserver(f.Events, f.Clock);
            var context = new SelfTestContext(_session, _assets, _graph, observer);
            try
            {
                await _graph.WhenRunning.AttachExternalCancellation(ct);
                var connectivity = _graph.Cloud.Connectivity;
                var initial = await connectivity.ProbeNow(ct);
                f.Log.Write(LogLevel.Info, "selftest",
                    $"evt=selftest.network reachable={_session.NetworkReachable} probe={initial}");
                await context.WaitUntil(() => connectivity.State == Connectivity.Online, 20, ct);
                LogFrames("launch");
                foreach (var (name, run) in Scenarios)
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
                _session.Profile?.End();
            }

            _session.SaveGraphicsStates(f.Log);
            var overall = Overall(results);
            ResultsWriter.Write(_resultsPath, SystemInfo.deviceModel, started, f.Usage.SpentUsd - spentBefore, overall, results);
            f.Log.Write(LogLevel.Info, "selftest",
                $"evt=selftest.done overall={overall} spent_usd={f.Usage.SpentUsd - spentBefore:0.0000}");
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
