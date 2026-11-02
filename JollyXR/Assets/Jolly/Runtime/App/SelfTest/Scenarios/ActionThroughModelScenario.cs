using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// The model's path to the body (ADR-0047): "Could you do a little dance for my friends?" is outside the guard's
    /// tables, so the model must call an action itself, and say something. <c>action_dance</c> and <c>action_move_behind</c>
    /// are in the tables, so since the guard they no longer reach the model.
    /// </summary>
    public static class ActionThroughModelScenario
    {
        public const string Name = "action_through_model";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.danceThroughModel, null, ct);
            TurnChecks.ModelAction(r, c.Observer.ToolsStarted, c.Observer.ToolsCompleted, c.Observer.ToolCallIds);
            return r;
        }
    }
}
