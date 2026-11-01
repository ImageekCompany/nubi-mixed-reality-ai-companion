using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// Requirement 23 with the network switched off: a press while Offline must play a baked line with a caption within
    /// 2 s (R5) and return to Idle; switching the network back must bring the app Online again.
    /// </summary>
    public static class OfflineGreetingScenario
    {
        public const string Name = "offline_greeting";
        const double MaxCaptionMs = 2000;

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = new ScenarioResult(Name);
            var connectivity = c.Graph.Cloud.Connectivity;
            var output = c.Graph.Speech.Output;
            var firstSampleAt = double.NaN;
            void OnFirstSample(int _)
            {
                if (double.IsNaN(firstSampleAt))
                    firstSampleAt = c.Clock.Now;
            }

            c.Session.ForceOffline = true;
            output.FirstSamplePlayed += OnFirstSample;
            try
            {
                var offline = await c.WaitUntil(() => connectivity.State == Connectivity.Offline, 5, ct);
                if (!r.Check(offline, "connectivity never reached Offline (state " + connectivity.State + ")"))
                    return r;
                c.Observer.Reset();
                var pressedAt = c.Clock.Now;
                c.Session.Voice.Press();
                c.Session.Voice.Release();
                await c.WaitUntil(() => c.Observer.Captions.Count > 0, 3, ct);
                var idle = await c.WaitForIdle(15, ct);
                var captionMs = (c.Observer.FirstCaptionAt - pressedAt) * 1000;
                r.Metric("caption_ms", captionMs).Metric("first_audio_ms", (firstSampleAt - pressedAt) * 1000)
                 .Metric("line", c.Observer.Captions.Count > 0 ? c.Observer.Captions[0] : null)
                 .Metric("states", c.Observer.StatePath());
                r.Check(captionMs <= MaxCaptionMs, "no caption within 2 s");
                r.Check(!double.IsNaN(firstSampleAt), "the canned line played no audio");
                r.Check(idle, "did not return to Idle");
            }
            finally
            {
                output.FirstSamplePlayed -= OnFirstSample;
                c.Session.ForceOffline = false;
            }

            await connectivity.ProbeNow(ct);
            var online = await c.WaitUntil(() => connectivity.State == Connectivity.Online, 15, ct);
            r.Metric("recovered", connectivity.State.ToString());
            r.Check(online, "did not come back Online");
            return r;
        }
    }
}
