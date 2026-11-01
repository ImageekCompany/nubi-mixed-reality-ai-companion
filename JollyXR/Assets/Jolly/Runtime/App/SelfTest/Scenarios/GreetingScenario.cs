using System.Threading;
using Cysharp.Threading.Tasks;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>"Hi Nubi, who are you?": a spoken answer with no key tool, and Nubi stays where it is.</summary>
    public static class GreetingScenario
    {
        public const string Name = "voice_greeting";

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.greeting, null, ct);
            TurnChecks.StayedPut(r, c.Observer.ToolsStarted);
            return r;
        }
    }
}
