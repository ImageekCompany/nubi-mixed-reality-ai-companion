using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Core.Tools;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// "Nubi, come here" must reach the come_here tool. Without a scanned floor the tool answers 'unreachable' and the
    /// model explains it; that is a graceful pass, reported so the environment is visible in the results.
    /// </summary>
    public static class ComeHereScenario
    {
        public const string Name = "voice_come_here";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.comeHere, ToolNames.ComeHere, ct);
            var nubi = c.Graph.Scene.nubi;
            var floor = c.Graph.Xr.World.HasWalkableArea;
            r.Metric("navmesh", floor ? "yes" : "no").Metric("nubi_mode", nubi.Mode.ToString())
             .Metric("nubi_moving", nubi.IsMoving ? "yes" : "no");
            if (!floor)
                r.Degrade("no walkable floor (Space Setup / NavMesh): the tool answered without moving");
            return r;
        }
    }
}
