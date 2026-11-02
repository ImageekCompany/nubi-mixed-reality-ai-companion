using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// The model's path to the body (ADR-0047): "Could you do a little dance for my friends?" is outside the guard's
    /// tables, so the model must call an action itself, and say something. <c>action_dance</c> and <c>action_move_behind</c>
    /// are in the tables, so since the guard they no longer reach the model. The model missed this phrase 1 time in 4, so
    /// one retry keeps a run green on a miss and red on a regression; the miss still degrades it and shows what it said.
    /// </summary>
    public static class ActionThroughModelScenario
    {
        public const string Name = "action_through_model";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.danceThroughModel, null, ct);
            if (!TurnChecks.ModelCalledAction(c.Observer.ToolsStarted, c.Observer.ToolCallIds))
            {
                r = await VoiceTurnScenario.Run(c, Name, c.Assets.danceThroughModel, null, ct);
                r.Degrade("the model called no action on the first try");
            }
            TurnChecks.ModelAction(r, c.Observer.ToolsStarted, c.Observer.ToolsCompleted, c.Observer.ToolCallIds);
            return r;
        }
    }
}
