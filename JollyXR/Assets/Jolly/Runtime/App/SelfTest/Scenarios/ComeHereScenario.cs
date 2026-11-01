using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// "Nubi, come here" must reach the come_here tool and get Nubi walking. Without Space Setup the room-scan flow
    /// gives a fallback floor around the user (ADR-0024), so the walk passes on it and navmesh=fallback says so. Only
    /// with no walkable area at all is it degraded: the tool then answers 'unreachable' and the model explains it.
    /// </summary>
    public static class ComeHereScenario
    {
        public const string Name = "voice_come_here";
        // The scan timeout (8 s after the scene permission) has normally passed by now; this covers a slow start.
        const double RoomSettleSeconds = 15;
        static readonly TimeSpan ArrivalWait = TimeSpan.FromSeconds(10);

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var room = c.Graph.Room;
            await c.WaitUntil(() => room.Status != RoomScanStatus.Pending, RoomSettleSeconds, ct);
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.comeHere, ToolNames.ComeHere, ct);
            var nubi = c.Graph.Scene.nubi;
            var area = c.Graph.Xr.World.WalkableArea;
            var outcome = await nubi.WhenArrived(ct).TimeoutWithoutException(ArrivalWait);
            r.Metric("navmesh", area.ToString().ToLowerInvariant()).Metric("room", room.Status.ToString())
             .Metric("walk", outcome.IsTimeout ? "still_walking" : outcome.Result.ToString())
             .Metric("nubi_mode", nubi.Mode.ToString());
            if (area == WalkableArea.None)
            {
                r.Degrade("no walkable floor (no Space Setup and no fallback floor): the tool answered without moving");
                return r;
            }
            r.Check(!outcome.IsTimeout && outcome.Result == MoveOutcome.Arrived, "Nubi did not walk to the user");
            return r;
        }
    }
}
