using Jolly.Contracts;
using Jolly.Core.Agent;
using Jolly.Core.Config;
using Jolly.Core.Conversation;
using Jolly.Core.Memory;
using Jolly.Core.Personas;
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
        public MemoryKeeper Memory { get; private set; }
        public ConversationMemory Conversation { get; private set; }
        public SceneMemory Scene { get; private set; }
        public ToolRegistry Tools { get; private set; }

        public static ConversationServices Build(Foundation f, JollyConfig config, CloudServices cloud, XrServices xr,
                                                 SpeechServices speech, PersonaRepository personas, ILocalizer localizer,
                                                 IUserPreferences preferences, ICharacterController character,
                                                 IVoiceActivation voice, IMaskPresenter masks, DisposableStack disposables)
        {
            var limits = new MemoryLimits();
            var timings = new ConversationTimings { AcknowledgeOnRelease = config.acknowledgeOnRelease };
            var conversation = new ConversationMemory(f.Paths.MemoryDirectory, personas, limits, f.Log);
            var scene = new SceneMemory(f.Paths.SceneMemory, xr.Anchors, f.Clock, limits, f.Log);
            var tools = ToolCatalog.Build(f, cloud, xr, character, scene, personas, localizer, masks, out var frames);
            var agent = new AgentLoop(cloud.VoiceChat, tools, conversation, scene, personas, localizer, preferences, f.Log,
                f.Clock, f.Latency, f.Events, new AgentSettings { SafetyIdentifier = f.SafetyIdentifier });

            var listener = new TurnListener(speech.Microphone, cloud.SpeechToText, frames, TranscriptAliases.Default,
                localizer, f.Events, f.Latency, f.Log, timings);
            var guards = new TurnGuards(new StopGuard(localizer), new CrisisGuard(localizer), character);
            var turnSpeech = disposables.Add(new TurnSpeech(speech.Queue, speech.Splitter, speech.Output, f.Machine,
                f.Events, f.Latency, f.Clock));
            var pipeline = new TurnPipeline(listener, guards, turnSpeech, agent, speech.Offline, personas, f.Machine,
                f.Events, f.Latency, localizer, f.Log, timings);
            var keeper = new MemoryKeeper(conversation, scene, cloud.BackgroundChat, personas, f.Log);
            var switcher = new PersonaSwitcher(personas, personas, conversation, character, f.Log);
            disposables.Add(new ConversationStateRelay(f.Machine, f.Events, character));
            var controller = disposables.Add(new ConversationController(voice,
                new TurnRouter(cloud.Connectivity, f.Usage), pipeline,
                new BargeInCoordinator(speech.Output, speech.Queue, turnSpeech, f.Latency), f.Machine, switcher, keeper,
                localizer, f.Clock, f.Log, timings));

            return new ConversationServices
            {
                Controller = controller, Memory = keeper, Conversation = conversation, Scene = scene, Tools = tools,
            };
        }
    }
}
