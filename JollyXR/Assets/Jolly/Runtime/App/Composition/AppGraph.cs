using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.App.Adapters;
using Jolly.Character;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Offline;
using Jolly.Core.Personas;
using Jolly.Core.Spatial;
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
        /// <summary><see cref="Localizer"/> with the selected character's name filled in; what everything else reads.</summary>
        public CharacterNamedLocalizer Text { get; private set; }
        public PersonaRepository Personas { get; private set; }
        public IUserPreferences Preferences { get; private set; }
        public CloudServices Cloud { get; private set; }
        public XrServices Xr { get; private set; }
        public SpeechServices Speech { get; private set; }
        public RoomScanMonitor Room { get; private set; }
        public ConversationServices Conversation { get; private set; }
        public IdleScheduler Idle { get; private set; }
        public LookTelemetry Look { get; private set; }
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
            var character = scene.nubi.Definition != null ? scene.nubi.Definition : config.defaultCharacter;
            Text = new CharacterNamedLocalizer(Localizer, NameOf(character), character.nameKey);
            Personas = d.Add(new PersonaRepository(PersonaDefinitions(config), config.defaultPersonaId, Text, character.voice));
            scene.settings.Construct(Text, Personas, f.Usage, new PlayerPrefsPreferencesStore(), f.Clock);
            Preferences = scene.settings;

            Cloud = CloudServices.Build(f, config, edges, d);
            Xr = XrServices.Build(f, config, scene, edges.WrapCamera, d);
            scene.pushToTalk.Construct(Xr.Hands, f.Clock, scene.head, Preferences);
            scene.controllerPushToTalk.Construct(Xr.Hands);
            Speech = SpeechServices.Build(f, config, character, scene, edges, Cloud.TextToSpeech, Text, Preferences, d);
            scene.nubi.Construct(Xr.User, Speech.LipSync, f.Clock, f.Random);
            BindViews(scene);

            var roomSettings = RoomSettingsFor(config, edges);
            Room = new RoomScanMonitor(Xr.SceneData, Xr.User, scene.nubi, f.Clock, f.Log, roomSettings,
                InteractionLinePlayer.CanSpeak(f.Machine, Speech.Output));
            var voice = edges.Voice ?? d.Add(new CompositeVoiceActivation(scene.pushToTalk, scene.controllerPushToTalk));
            Conversation = ConversationServices.Build(f, config, Cloud, Xr, Speech, Personas, Text,
                TranscriptAliases.ForName(Text.Name, character.aliases), Preferences, scene.nubi, voice,
                edges.WrapMasks(scene.maskOverlay), Room, new RoomContext(Room, Xr.World, Xr.User, roomSettings),
                edges.ModelReplies, d);
            d.Add(new StatusRelay(Cloud.Connectivity, scene.permissions, scene.status));
            Look = d.Add(new LookTelemetry(f.Machine, scene.nubi, Xr.User, f.Log));
            d.Add(new SettingsRouter(scene.settings, Personas, Text, Conversation.Controller, f.Log, appCt));
            d.Add(new SettingsLauncher(scene.settingsWindow, scene.wristAnchor, scene.wristMenu, scene.menuButton,
                Conversation.Guards, f.Events));
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
            scene.toolBadges.Construct(f.Events, Text, f.Clock);
            scene.latencyHud.Construct(f.Events, f.Latency, Text);
            scene.maskOverlay.Construct(f.Clock, f.Events);
            scene.status.Construct(Text);
            scene.settingsWindow.Construct(f.Clock, scene.head, f.Log);
            scene.wristMenu.Construct(Text);
            scene.wristAnchor.Construct(Xr.Hands, scene.head, f.Clock);
        }

        void BindInteractions(SceneRefs scene, CancellationToken appCt)
        {
            var f = Foundation;
            scene.interactionSignals.Construct(Xr.User, Xr.Hands, f.Clock, scene.nubiCollider, scene.nubiInteractable);
            var triggers = _disposables.Add(new InteractionTriggers(scene.interactionSignals, f.Clock,
                InteractionLinePlayer.CanSpeak(f.Machine, Speech.Output)));
            var lines = _disposables.Add(new InteractionLinePlayer(triggers, Speech.Offline, f.Machine, scene.nubi, f.Log, appCt));
            Action offer = () => lines.Request(LineCategory.RoomScanOffer);
            Room.OfferScan += offer;
            _disposables.Add(new Subscription(() => Room.OfferScan -= offer));
        }

        /// <summary>Permissions first (the microphone and camera wait for them), then the long-running loops.</summary>
        public async UniTask Run(CancellationToken appCt)
        {
            await Scene.permissions.Run(appCt);
            Loop("connectivity", Cloud.Connectivity.RunAsync(appCt), appCt).Forget();
            Loop("warmup", StartupWarmup.Run(Conversation.Frames, Cloud.RealtimeAsr, Cloud.Wit, Personas.Current?.Voice, Foundation.Clock,
                Foundation.Log, appCt),
                appCt).Forget();
            Loop("idle", Idle.RunAsync(IsIdle, appCt), appCt).Forget();
            Loop("room", Room.RunAsync(appCt), appCt).Forget();
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

        /// <summary>The app is back in focus, possibly from Space Setup: read the scene again.</summary>
        public void Resume() => Room?.OnResumed();

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

        static RoomSettings RoomSettingsFor(JollyConfig config, Edges edges) => new()
        {
            ScanTimeout = TimeSpan.FromSeconds(config.roomScanTimeoutSeconds),
            FallbackFloor = config.fallbackFloor,
            FallbackRadius = config.fallbackFloorRadius,
            OfferScan = config.offerRoomScan && edges.OfferRoomScan,
        };

        /// <summary>The display name, else the id, so a definition with no name still names itself.</summary>
        static string NameOf(CharacterDefinition character) =>
            string.IsNullOrWhiteSpace(character.displayName) ? character.id : character.displayName;

        static PersonaDefinition[] PersonaDefinitions(JollyConfig config)
        {
            var definitions = new PersonaDefinition[config.personas.Length];
            for (var i = 0; i < definitions.Length; i++)
                definitions[i] = config.personas[i].ToDefinition();
            return definitions;
        }
    }
}
