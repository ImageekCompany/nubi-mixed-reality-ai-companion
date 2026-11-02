using System;
using Jolly.App.Platform;
using Jolly.Contracts;
using Jolly.Core.Agent;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Location;
using Jolly.Core.Memory;
using Jolly.Core.Personas;
using Jolly.Core.Spatial;
using Jolly.Core.Tools;

namespace Jolly.App.Composition
{
    /// <summary>
    /// The conversation core (architecture §3, §5.1–5.2, §6–8): memories, the agent loop with every tool, the turn
    /// pipeline and the controller that owns the session and turn tokens.
    /// </summary>
    public sealed class ConversationServices
    {
        public ConversationController Controller { get; private set; }
        /// <summary>The local reactions to a final; raises the voice settings command.</summary>
        public TurnGuards Guards { get; private set; }
        public MemoryKeeper Memory { get; private set; }
        public ConversationMemory Conversation { get; private set; }
        public SceneMemory Scene { get; private set; }
        public ToolRegistry Tools { get; private set; }
        /// <summary>The camera grabber shared by the PTT-release pre-capture and the vision tools.</summary>
        public FrameGrabber Frames { get; private set; }

        public static ConversationServices Build(Foundation f, JollyConfig config, CloudServices cloud, XrServices xr,
                                                 SpeechServices speech, PersonaRepository personas, ILocalizer localizer,
                                                 TranscriptAliases aliases, IUserPreferences preferences, ICharacterController character,
                                                 IVoiceActivation voice, IAudioCapture microphone, IMaskPresenter masks, IRoomScan room,
                                                 RoomContext roomContext, Action<string> modelReplies, DisposableStack disposables)
        {
            var limits = new MemoryLimits();
            var timings = new ConversationTimings { AcknowledgeQuestions = config.acknowledgeQuestions };
            var conversation = new ConversationMemory(f.Paths.MemoryDirectory, personas, limits, f.Log);
            var scene = new SceneMemory(f.Paths.SceneMemory, xr.Anchors, f.Clock, limits, f.Log);
            var tools = ToolCatalog.Build(f, config, cloud, xr, character, scene, personas, localizer, masks, room, out var frames);
            var location = new UserLocation(preferences, new DeviceRegion(f.Log), f.Clock);
            var agent = new AgentLoop(cloud.VoiceChat, tools, conversation, scene, roomContext, personas, localizer, preferences, f.Log,
                f.Clock, f.Latency, f.Events, new AgentSettings
                {
                    SafetyIdentifier = f.SafetyIdentifier,
                    HoldLineAfter = TimeSpan.FromSeconds(config.holdLineSeconds),
                    HoldLineRepeat = TimeSpan.FromSeconds(config.holdLineRepeatSeconds),
                    ReplyObserved = modelReplies,
                }, new WebSearchPolicy(location, cloud.Connectivity, config.webSearch));

            var listener = new TurnListener(microphone, cloud.SpeechToText, frames, aliases,
                localizer, f.Events, f.Latency, f.Log, timings);
            var guards = new TurnGuards(new StopGuard(localizer), new ComeGuard(localizer), new ActionGuard(localizer), new SettingsGuard(localizer),
                new CrisisGuard(localizer), new QuestionCue(localizer), character, tools, personas, preferences, f.Events, f.Log, f.Clock);
            var turnSpeech = disposables.Add(new TurnSpeech(speech.Queue, speech.Splitter, speech.Output, f.Machine,
                f.Events, f.Latency, f.Clock));
            var pipeline = new TurnPipeline(listener, guards, turnSpeech, agent, speech.Offline, conversation, personas,
                f.Machine, f.Events, f.Latency, localizer, f.Log, timings);
            var keeper = new MemoryKeeper(conversation, scene, cloud.BackgroundChat, personas, f.Log);
            var switcher = new PersonaSwitcher(personas, personas, conversation, character, f.Log);
            disposables.Add(new ConversationStateRelay(f.Machine, f.Events, character));
            disposables.Add(new SearchThinkGesture(f.Events, character));
            var controller = disposables.Add(new ConversationController(voice,
                new TurnRouter(cloud.Connectivity, f.Usage), pipeline,
                new BargeInCoordinator(speech.Output, speech.Queue, turnSpeech, f.Latency), f.Machine, switcher, keeper,
                localizer, f.Clock, f.Log, timings));

            return new ConversationServices
            {
                Controller = controller, Guards = guards, Memory = keeper, Conversation = conversation, Scene = scene, Tools = tools,
                Frames = frames,
            };
        }
    }
}
