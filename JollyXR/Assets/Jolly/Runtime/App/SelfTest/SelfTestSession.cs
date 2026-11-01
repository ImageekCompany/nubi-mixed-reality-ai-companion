using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Composition;
using Jolly.Contracts;
using Jolly.XR.Capture;

namespace Jolly.App.SelfTest
{
    /// <summary>
    /// The device self-test (requirements: self-checking pipeline). Compiled into every build but inert unless
    /// <c>persistentDataPath/selftest.json</c> exists (or, off-device, the <c>-selftest</c> argument is given). The flag is
    /// consumed at once, so the next normal launch is a normal app. There is deliberately no intent path on Android: the
    /// launcher activity is exported, so any app could start a paid, offline-forcing run; only adb can write the flag. It replaces only the edges a person would provide
    /// (voice and the pinch, microphone, a lit room, the network switch); everything between them runs for real.
    /// </summary>
    public sealed class SelfTestSession
    {
        public const string FlagFile = "selftest.json";
        public const string ResultsFile = "selftest_results.json";
        const string CommandLineFlag = "-selftest";

        readonly SelfTestAssets _assets;
        readonly string _resultsPath;
        Func<bool> _realReachability;

        SelfTestSession(SelfTestAssets assets, string dataRoot)
        {
            _assets = assets;
            _resultsPath = Path.Combine(dataRoot, ResultsFile);
        }

        public bool ForceOffline { get; set; }
        /// <summary>The platform's own reachability check, ignoring <see cref="ForceOffline"/>.</summary>
        public bool NetworkReachable => _realReachability();
        public ClipMicrophone Microphone { get; private set; }
        public ScriptedPushToTalk Voice { get; private set; }
        public SwitchableCameraSource Camera { get; private set; }
        /// <summary>Started by the bootstrap so the launch is traced too; disposes itself when the app token cancels.</summary>
        public FrameSpikeLog Frames { get; private set; }

        /// <summary>Null in normal use. Otherwise rewires <paramref name="edges"/> and returns the armed session.</summary>
        public static SelfTestSession TryStart(Edges edges, SelfTestAssets assets, Foundation foundation, SceneRefs scene)
        {
            var flag = Path.Combine(edges.Paths.DataRoot, FlagFile);
            if (!ConsumeFlag(flag) && !HasCommandLineFlag())
                return null;
            var log = foundation.Log;
            if (assets == null)
            {
                log.Write(LogLevel.Error, "selftest", "evt=selftest.no_assets");
                return null;
            }
            var session = new SelfTestSession(assets, edges.Paths.DataRoot) { Frames = new FrameSpikeLog(log) };
            if (File.Exists(session._resultsPath))
                File.Delete(session._resultsPath);
            log.Write(LogLevel.Info, "selftest", "evt=selftest.armed");
            session.Rewire(edges, foundation, scene);
            return session;
        }

        public UniTaskVoid Run(AppGraph graph, CancellationToken ct) =>
            new SelfTestRunner(this, _assets, graph, _resultsPath).Run(ct);

        void Rewire(Edges edges, Foundation foundation, SceneRefs scene)
        {
            _realReachability = edges.IsNetworkReachable;
            edges.IsNetworkReachable = () => !ForceOffline && _realReachability();
            Microphone = new ClipMicrophone(edges.Microphone, foundation.Clock);
            edges.Microphone = Microphone;
            Voice = new ScriptedPushToTalk();
            edges.Voice = Voice;
            var wrap = edges.WrapCamera;
            var still = _assets.fallbackStill != null ? _assets.fallbackStill.bytes : Array.Empty<byte>();
            edges.WrapCamera = live => Camera = new SwitchableCameraSource(wrap(live),
                new EditorStillFrameSource(still, scene.userPose, foundation.Clock));
        }

        static bool ConsumeFlag(string path)
        {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }

        /// <summary>Not on Android, where Unity fills the command line from the launching intent's <c>unity</c> extra.</summary>
        static bool HasCommandLineFlag()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return false;
#else
            return Array.IndexOf(Environment.GetCommandLineArgs(), CommandLineFlag) >= 0;
#endif
        }
    }
}
