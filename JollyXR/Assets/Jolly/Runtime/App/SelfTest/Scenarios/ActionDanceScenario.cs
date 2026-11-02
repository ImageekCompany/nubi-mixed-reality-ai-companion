using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Core.Tools;
using UnityEngine;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// The guard path of a short command (ADR-0047): "Show me how to dance!" must run actions.dance with no model round
    /// and the Animator must actually enter the Dance state. <see cref="ActionThroughModelScenario"/> is the model's.
    /// A character without the Dance clip is not offered the action, so the scenario is skipped for it.
    /// </summary>
    public static class ActionDanceScenario
    {
        public const string Name = "action_dance";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var nubi = c.Graph.Scene.nubi;
            if (!nubi.CanPlay(GestureId.Dance))
                return new ScenarioResult(Name) { Status = ScenarioStatus.Skipped };

            using var watch = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var danced = SawState(nubi.Animator, NubiAnimatorParameters.ActionLayer, "Dance", watch.Token);
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.dance, ToolNames.Actions.Dance, ct);
            watch.Cancel();
            var seen = await danced;
            r.Metric("dance_state", seen ? "played" : "never");
            r.Check(seen, "the Dance state never played");
            TurnChecks.GuardPath(r, TurnChecks.ActionSource(c.Observer.ToolCallIds, c.Observer.Replies.Count));
            return r;
        }

        /// <summary>Samples the layer every frame until cancelled; true if <paramref name="state"/> was ever current.</summary>
        static async UniTask<bool> SawState(Animator animator, string layerName, string state, CancellationToken ct)
        {
            var layer = animator.GetLayerIndex(layerName);
            var hash = Animator.StringToHash(state);
            while (!ct.IsCancellationRequested)
            {
                if (layer >= 0 && animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == hash)
                    return true;
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    break;
            }
            return false;
        }
    }
}
