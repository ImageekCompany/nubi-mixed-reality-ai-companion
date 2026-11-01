using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Jolly.App;
using Jolly.App.Composition;
using Jolly.App.Platform;
using Jolly.Contracts;
using Jolly.Testing;
using Jolly.Tests.PlayMode.E2E.Doubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jolly.Tests.PlayMode.E2E
{
    /// <summary>
    /// Main.unity with its real bootstrap and graph, and doubles only at the provider edges: HTTP, WebSocket, the health
    /// status call, Wit, the microphone, the speaker and push-to-talk. Editor only (scenes load through the AssetDatabase).
    /// </summary>
    sealed class AppHarness
    {
        const string ScenePath = "Assets/Jolly/Scenes/Main.unity";
        const double BootSeconds = 15;

        readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "jolly-e2e-" + Guid.NewGuid().ToString("N"));
        Scene _scene;

        public RoutingHttpTransport Http { get; } = new();
        public MuseAsrServer Asr { get; } = new();
        public StatusClient Status { get; } = new();
        public ToneTextToSpeech Tts { get; } = new();
        public SimulatedSpeechOutput Speaker { get; } = new();
        public FakeVoiceActivation Voice { get; } = new();
        public bool NetworkUp { get; set; } = true;
        public JollyBootstrap Bootstrap { get; private set; }
        public AppGraph Graph => Bootstrap.Graph;
        public IClock Clock => Graph.Foundation.Clock;
        public ConversationState State => Graph.Foundation.Machine.State;

        public List<ConversationState> States { get; } = new();
        public List<string> Captions { get; } = new();
        public List<string> ToolsStarted { get; } = new();
        public List<string> ToolsCompleted { get; } = new();
        public string UserFinal { get; private set; }

        public static async UniTask<AppHarness> Start(bool online = true)
        {
            var harness = new AppHarness { NetworkUp = online };
            harness.Status.Online = online;
            try
            {
                await harness.Load();
            }
            catch
            {
                await harness.Unload();
                throw;
            }
            return harness;
        }

        async UniTask Load()
        {
#if UNITY_EDITOR
            Directory.CreateDirectory(_dataRoot);
            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
            }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
            Assert.That(Bootstrap != null, Is.True, "Main.unity has a JollyBootstrap");
            var deadline = Time.realtimeSinceStartupAsDouble + BootSeconds;
            while (Bootstrap.Graph == null && Time.realtimeSinceStartupAsDouble < deadline)
                await UniTask.Yield();
            Assert.That(Bootstrap.Graph, Is.Not.Null, "the graph was built");
            await Graph.WhenRunning.Timeout(TimeSpan.FromSeconds(BootSeconds));
            Observe(Graph.Foundation.Events);
#else
            Assert.Ignore("E2E tests load Main.unity through the AssetDatabase (Editor only).");
            await UniTask.CompletedTask;
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ScenePath)
                return;
            _scene = scene;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<JollyBootstrap>(out var bootstrap))
                    Bootstrap = bootstrap;
            }
            Bootstrap?.UseEdges(Edges());
        }

        Edges Edges() => new()
        {
            Paths = new AppPaths(_dataRoot),
            Secrets = new InMemorySecrets().With(SecretKeys.MetaApiKey, "LLM_0_e2e-test-key")
                                           .With(SecretKeys.WitClientToken, "e2e-wit-token"),
            IsNetworkReachable = () => NetworkUp,
            Http = Http,
            WebSockets = Asr.Connect,
            HttpStatus = Status,
            TextToSpeech = Tts,
            Microphone = new SilentMicrophone(),
            Voice = Voice,
            SpeechOutput = Speaker,
        };

        void Observe(IConversationEvents events)
        {
            events.StateChanged += (_, to) => States.Add(to);
            events.CaptionStarted += (_, text) => Captions.Add(text);
            events.ToolStarted += (_, name) => ToolsStarted.Add(name);
            events.ToolCompleted += (_, name, ok) => ToolsCompleted.Add(ok ? name : name + ":failed");
            events.UserFinal += text => UserFinal = text;
        }

        public async UniTask Talk(double holdSeconds = 0.3)
        {
            Voice.Press();
            await UniTask.Delay(TimeSpan.FromSeconds(holdSeconds), DelayType.Realtime);
            Voice.Release();
        }

        public async UniTask<bool> WaitUntil(Func<bool> condition, double seconds)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline)
                    return false;
                await UniTask.Yield();
            }
            return true;
        }

        public UniTask<bool> WaitForIdle(double seconds) =>
            WaitUntil(() => State == ConversationState.Idle && !Speaker.IsPlaying, seconds);

        public string StatePath() => string.Join(">", States);

        public async UniTask Unload()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                await SceneManager.UnloadSceneAsync(_scene);
            if (Directory.Exists(_dataRoot))
                Directory.Delete(_dataRoot, true);
        }
    }
}
