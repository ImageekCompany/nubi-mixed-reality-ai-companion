using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// Architecture §5.2: a press while Nubi speaks must silence the output within 150 ms. Silence is read from the level
    /// the audio thread rendered, so it includes the 20 ms fade and the DSP buffer, not just the flush call.
    /// </summary>
    public static class BargeInScenario
    {
        public const string Name = "barge_in";
        const double MaxSilenceMs = 150;
        const float SilentLevel = 0.002f;
        static readonly TimeSpan SpeakBeforeBargeIn = TimeSpan.FromSeconds(0.6);

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = new ScenarioResult(Name);
            var level = c.Graph.Scene.speechOutput.Level;
            var output = c.Graph.Speech.Output;
            await c.WaitForIdle(20, ct);
            c.Observer.Reset();
            await c.SpeakClip(c.Assets.longAnswer.clip, ct);
            var speaking = await c.WaitUntil(() => c.State == ConversationState.Speaking && level.Level > SilentLevel, 20, ct);
            if (!r.Check(speaking, "Nubi never started speaking"))
                return r;
            await c.Clock.Delay(SpeakBeforeBargeIn, ct);

            var pressedAt = c.Clock.Now;
            c.Session.Microphone.ArmSilence();
            c.Session.Voice.Press();
            await c.WaitUntil(() => !output.IsPlaying, 1, ct);
            var flushedMs = (c.Clock.Now - pressedAt) * 1000;
            var silent = await c.WaitUntil(() => level.Level <= SilentLevel, 1, ct);
            var silenceMs = (c.Clock.Now - pressedAt) * 1000;
            c.Session.Voice.Release();
            var idle = await c.WaitForIdle(15, ct);

            r.Metric("flush_ms", flushedMs).Metric("silence_ms", silent ? silenceMs : double.NaN)
             .Metric("state_after_press", c.Observer.StatePath());
            r.Check(silent && silenceMs <= MaxSilenceMs, "output not silent within 150 ms of the press");
            r.Check(c.Observer.States.Contains(ConversationState.Listening), "the press did not start Listening");
            r.Check(idle, "did not return to Idle after the empty barge-in turn");
            return r;
        }
    }
}
