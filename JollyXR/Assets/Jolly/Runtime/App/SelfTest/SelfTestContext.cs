using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Composition;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App.SelfTest
{
    /// <summary>What a scenario drives and observes, plus the scripted voice turn they share.</summary>
    public sealed class SelfTestContext
    {
        // After the clip ends, so the ASR hears its last word before the release.
        const double ReleaseTailSeconds = 0.4;

        public SelfTestContext(SelfTestSession session, SelfTestAssets assets, AppGraph graph, TurnObserver observer)
        {
            Session = session;
            Assets = assets;
            Graph = graph;
            Observer = observer;
        }

        public SelfTestSession Session { get; }
        public SelfTestAssets Assets { get; }
        public AppGraph Graph { get; }
        public TurnObserver Observer { get; }
        public IClock Clock => Graph.Foundation.Clock;
        public ILog Log => Graph.Foundation.Log;
        public ConversationState State => Graph.Foundation.Machine.State;

        /// <summary>True when <paramref name="condition"/> held within <paramref name="timeoutSeconds"/>.</summary>
        public async UniTask<bool> WaitUntil(Func<bool> condition, double timeoutSeconds, CancellationToken ct)
        {
            var deadline = Clock.Now + timeoutSeconds;
            while (!condition())
            {
                if (Clock.Now > deadline)
                    return false;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            return true;
        }

        /// <summary>Speaks <paramref name="clip"/> into the pipeline exactly as a pinch-and-talk would.</summary>
        public async UniTask SpeakClip(AudioClip clip, CancellationToken ct)
        {
            Session.Microphone.Arm(clip);
            Session.Voice.Press();
            await Clock.Delay(TimeSpan.FromSeconds(clip.length + ReleaseTailSeconds), ct);
            Session.Voice.Release();
        }

        public UniTask<bool> WaitForIdle(double timeoutSeconds, CancellationToken ct) =>
            WaitUntil(() => State == ConversationState.Idle && !Graph.Speech.Output.IsPlaying, timeoutSeconds, ct);
    }
}
