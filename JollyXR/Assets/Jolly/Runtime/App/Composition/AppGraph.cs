using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Adapters;
using Jolly.Contracts;
using Jolly.Core.Conversation;
using Jolly.Core.Offline;
using Jolly.Core.Personas;
using Jolly.Localization;
using Jolly.Presentation;

namespace Jolly.App.Composition
{
    /// <summary>
    /// The object graph of the app (architecture §3, ADR-0009), built in dependency order: the localizer is initialised
    /// before anything reads text, settings before anything reads preferences. Owns every disposable it creates.
    /// </summary>
    public sealed class AppGraph : IDisposable
    {
        readonly DisposableStack _disposables;
        readonly UniTaskCompletionSource _running = new();

        AppGraph(Foundation foundation)
        {
            Foundation = foundation;
            _disposables = new DisposableStack(foundation.Log);
            _disposables.Add(foundation.Usage);
        }

        public Foundation Foundation { get; }
        public UnityLocalizer Localizer { get; private set; }
        public PersonaRepository Personas { get; private set; }
        public IUserPreferences Preferences { get; private set; }
        public CloudServices Cloud { get; private set; }
        public XrServices Xr { get; private set; }
        public SpeechServices Speech { get; private set; }
        public ConversationServices Conversation { get; private set; }
        public IdleScheduler Idle { get; private set; }
        public SceneRefs Scene { get; private set; }
        /// <summary>Completes once permissions are settled and the loops have started.</summary>
        public UniTask WhenRunning => _running.Task;

        public static async UniTask<AppGraph> Build(Foundation foundation, JollyConfig config, SceneRefs scene, Edges edges,
                                                    CancellationToken appCt)
        {
            var graph = new AppGraph(foundation) { Scene = scene };
            try
            {
                await graph.BuildServices(config, scene, edges, appCt);
                return graph;
            }
            catch
            {
                graph.Dispose();
                throw;
            }
        }

        async UniTask BuildServices(JollyConfig config, SceneRefs scene, Edges edges, CancellationToken appCt)
        {
            var f = Foundation;
            var d = _disposables;
            Localizer = d.Add(new UnityLocalizer(config.localeVoices, config.preloadTables, f.Log));
            await Localizer.Initialize(appCt);
            Personas = d.Add(new PersonaRepository(PersonaDefinitions(config), config.defaultPersonaId, Localizer));
            scene.settings.Construct(Localizer, Personas, f.Usage, new PlayerPrefsPreferencesStore(), f.Clock);
            Preferences = scene.settings;

            Cloud = CloudServices.Build(f, config, edges, d);
            Xr = XrServices.Build(f, config, scene, edges.WrapCamera, d);
            scene.pushToTalk.Construct(Xr.Hands, f.Clock, scene.head, Preferences);
            scene.controllerPushToTalk.Construct(Xr.Hands);
            Speech = SpeechServices.Build(f, config, scene, edges, Cloud.TextToSpeech, Localizer, Preferences, d);
            scene.nubi.Construct(Xr.User, Speech.LipSync, f.Clock, f.Random);
            BindViews(scene);

            var voice = edges.Voice ?? d.Add(new CompositeVoiceActivation(scene.pushToTalk, scene.controllerPushToTalk));
            Conversation = ConversationServices.Build(f, config, Cloud, Xr, Speech, Personas, Localizer, Preferences,
                scene.nubi, voice, scene.maskOverlay, d);
            d.Add(new StatusRelay(Cloud.Connectivity, scene.permissions, scene.status));
            d.Add(new SettingsRouter(scene.settings, Personas, Localizer, Conversation.Controller, f.Log, appCt));
            var offline = Speech.Offline;
            offline.CaptionStarted += f.Events.RaiseCaptionStarted;
            d.Add(new Subscription(() => offline.CaptionStarted -= f.Events.RaiseCaptionStarted));
            BindInteractions(scene, appCt);
            Idle = new IdleScheduler(scene.nubi, f.Clock, f.Random, new IdleConfig());
        }

        void BindViews(SceneRefs scene)
        {
            var f = Foundation;
            scene.captions.Construct(f.Events, Preferences, f.Clock);
            scene.toolBadges.Construct(f.Events, Localizer, f.Clock);
            scene.latencyHud.Construct(f.Events, f.Latency, Localizer);
            scene.maskOverlay.Construct(f.Clock, f.Events);
            scene.status.Construct(Localizer);
        }

        void BindInteractions(SceneRefs scene, CancellationToken appCt)
        {
            var f = Foundation;
            scene.interactionSignals.Construct(Xr.User, Xr.Hands, f.Clock, scene.nubiCollider, scene.nubiInteractable);
            var triggers = _disposables.Add(new InteractionTriggers(scene.interactionSignals, f.Clock,
                InteractionLinePlayer.CanSpeak(f.Machine, Speech.Output)));
            _disposables.Add(new InteractionLinePlayer(triggers, Speech.Offline, f.Machine, scene.nubi, f.Log, appCt));
        }

        /// <summary>Permissions first (the microphone and camera wait for them), then the long-running loops.</summary>
        public async UniTask Run(CancellationToken appCt)
        {
            await Scene.permissions.Run(appCt);
            Loop("connectivity", Cloud.Connectivity.RunAsync(appCt), appCt).Forget();
            Loop("idle", Idle.RunAsync(IsIdle, appCt), appCt).Forget();
            var conversation = Conversation.Controller.RunAsync(appCt);
            _running.TrySetResult();
            await conversation;
        }

        /// <summary>Quest does not reliably call OnDestroy, so pause and focus loss persist what matters now.</summary>
        public void Flush()
        {
            Foundation.Usage.Flush();
            Conversation?.Memory.Save(CancellationToken.None).Forget();
        }

        public void Dispose() => _disposables.Dispose();

        bool IsIdle() => Foundation.Machine.State == ConversationState.Idle && !Speech.Output.IsPlaying;

        async UniTaskVoid Loop(string name, UniTask loop, CancellationToken appCt)
        {
            try
            {
                await loop;
            }
            catch (OperationCanceledException) when (appCt.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                Foundation.Log.Write(LogLevel.Error, "app", "evt=loop.crashed loop=" + name, e);
            }
        }

        static PersonaDefinition[] PersonaDefinitions(JollyConfig config)
        {
            var definitions = new PersonaDefinition[config.personas.Length];
            for (var i = 0; i < definitions.Length; i++)
                definitions[i] = config.personas[i].ToDefinition();
            return definitions;
        }
    }
}
