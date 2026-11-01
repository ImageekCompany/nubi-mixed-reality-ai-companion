using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// "Hi Nubi, come here", the demo's opening line, must start come_here and get Nubi walking, from the local
    /// ComeGuard or the model (ADR-0036); come_source says which. Without Space Setup the room-scan flow
    /// gives a fallback floor around the user (ADR-0025), so the walk passes on it and navmesh=fallback says so. Only
    /// with no walkable area at all is it degraded: the tool then answers 'unreachable' and the model explains it.
    /// The guard's walk answers with a canned line and no model round (ADR-0037): a model request, or first audio
    /// later than <see cref="GuardFirstAudioBudgetMs"/> after release, degrades.
    /// </summary>
    public static class ComeHereScenario
    {
        public const string Name = "voice_come_here";
        public const double GuardFirstAudioBudgetMs = 600;
        // The scan timeout (8 s after the scene permission) has normally passed by now; this covers a slow start.
        const double RoomSettleSeconds = 15;
        static readonly TimeSpan ArrivalWait = TimeSpan.FromSeconds(10);

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var room = c.Graph.Room;
            await c.WaitUntil(() => room.Status != RoomScanStatus.Pending, RoomSettleSeconds, ct);
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.comeHere, ToolNames.ComeHere, ct);
            var latency = c.Graph.Foundation.Latency;
            var firstAudio = latency.Between(LatencyMark.PttReleased, LatencyMark.FirstAudioPlayed);
            var modelCalled = !double.IsNaN(latency.Between(LatencyMark.PttReleased, LatencyMark.LlmRequestSent));
            var nubi = c.Graph.Scene.nubi;
            var area = c.Graph.Xr.World.WalkableArea;
            var outcome = await nubi.WhenArrived(ct).TimeoutWithoutException(ArrivalWait);
            r.Metric("navmesh", area.ToString().ToLowerInvariant()).Metric("room", room.Status.ToString())
             .Metric("walk", outcome.IsTimeout ? "still_walking" : outcome.Result.ToString())
             .Metric("nubi_mode", nubi.Mode.ToString())
             .Metric("come_source", TurnChecks.ComeHereSource(c.Observer.ToolsStarted, c.Observer.ToolCallIds))
             .Metric("llm_request", modelCalled ? "sent" : "none");
            if (area == WalkableArea.None)
            {
                r.Degrade("no walkable floor (no Space Setup and no fallback floor): the tool answered without moving");
                return r;
            }
            r.Check(!outcome.IsTimeout && outcome.Result == MoveOutcome.Arrived, "Nubi did not walk to the user");
            if (modelCalled)
                r.Degrade("the turn sent a model request although the guard's walk answers it locally");
            if (!(firstAudio <= GuardFirstAudioBudgetMs))
                r.Degrade("first audio over the 600 ms budget of a guard walk");
            return r;
        }
    }
}
