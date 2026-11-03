using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>A short command's tool call, ready to run (<see cref="TurnGuards.TryMatchAction"/>).</summary>
    public sealed class GuardedAction
    {
        internal GuardedAction(string name, string argumentsJson, ITool tool, IToolArguments arguments, PersonaProfile persona)
        {
            Name = name;
            ArgumentsJson = argumentsJson;
            Tool = tool;
            Arguments = arguments;
            Persona = persona;
            CallId = TurnGuards.ActionCallPrefix + Guid.NewGuid().ToString("N");
        }

        public string Name { get; }
        public string ArgumentsJson { get; }
        public string CallId { get; }
        /// <summary>What to say once it has run, or null: a body move speaks for itself, "Got it" and "There!" do not.</summary>
        public string Reply { get; internal set; }
        internal ITool Tool { get; }
        internal IToolArguments Arguments { get; }
        internal PersonaProfile Persona { get; }
    }

    /// <summary>The local, model-free reactions to a final transcript (architecture §5.1, §9).</summary>
    public sealed class TurnGuards
    {
        /// <summary>Appended to the user text after the hand-off line has been spoken, so the reply follows it up.</summary>
        public const string CrisisNote =
            "\n\n(Note: emergency and crisis-line contacts were just read out to the user. Respond with warmth, take them " +
            "seriously, and encourage them to reach out to those services or someone they trust now.)";

        /// <summary>The call id of the guard's come_here on <see cref="ConversationEvents.ToolStarted"/>; the model's
        /// calls carry the provider's ids.</summary>
        public const string ComeHereCallId = "guard_come_here";

        /// <summary>Starts the call id of a guard's body action. Unique per call, since memory replays them to the model.</summary>
        public const string ActionCallPrefix = "guard_action_";

        readonly StopGuard _stop;
        readonly ComeGuard _come;
        readonly ActionGuard _actions;
        readonly SettingsGuard _settings;
        readonly PlaceGuard _places;
        readonly ILocalizer _localizer;
        readonly Func<PersonaProfile, string> _personaInstructions;
        readonly CrisisGuard _crisis;
        readonly QuestionCue _question;
        readonly ICharacterController _character;
        readonly IToolRegistry _tools;
        readonly IPersonaProvider _personas;
        readonly IUserPreferences _preferences;
        readonly ConversationEvents _events;
        readonly ILog _log;
        readonly IClock _clock;

        public TurnGuards(StopGuard stop, ComeGuard come, ActionGuard actions, SettingsGuard settings, CrisisGuard crisis, QuestionCue question, ICharacterController character,
                          IToolRegistry tools, IPersonaProvider personas, IUserPreferences preferences,
                          ConversationEvents events, ILog log, IClock clock, PlaceGuard places = null, ILocalizer localizer = null,
                          Func<PersonaProfile, string> personaInstructions = null)
        {
            _places = places;
            _localizer = localizer;
            _personaInstructions = personaInstructions;
            _stop = stop;
            _come = come;
            _actions = actions;
            _settings = settings;
            _crisis = crisis;
            _question = question;
            _character = character;
            _tools = tools;
            _personas = personas;
            _preferences = preferences;
            _events = events;
            _log = log;
            _clock = clock;
        }

        /// <summary>A short "open settings" (true) or "close settings" (false); the composition root shows or hides the panel.</summary>
        public event Action<bool> SettingsRequested;

        /// <summary>Halts locomotion on a short "stop"; the turn still goes to the model.</summary>
        public bool ApplyStop(string transcript)
        {
            if (!_stop.Matches(transcript))
                return false;
            _character.Stop();
            return true;
        }

        /// <summary>
        /// Runs the come_here tool on a short "come here" when the persona is offered it (its allow-list is a privacy
        /// promise, as for the model's calls). True when Nubi is walking: the pipeline then answers with a canned line and
        /// no model round (ADR-0037). An unreachable target returns false, so the model's own call explains it.
        /// </summary>
        public async UniTask<bool> ApplyComeHere(string transcript, CancellationToken ct)
        {
            if (!_come.Matches(transcript) || !IsOffered(ToolNames.ComeHere, out var persona)
                || !_tools.TryGet(ToolNames.ComeHere, out var tool)
                || !_tools.TryValidate(ToolNames.ComeHere, "{}", out var args, out _))
                return false;

            _events.RaiseToolStarted(ComeHereCallId, ToolNames.ComeHere);
            var result = await Execute(tool, ToolNames.ComeHere, args, new ToolContext { Persona = persona, Preferences = _preferences }, ct);
            _events.RaiseToolCompleted(ComeHereCallId, ToolNames.ComeHere, result.Ok);
            var walking = result.Ok && _character.Mode == LocomotionMode.MovingTo;
            _log.Write(LogLevel.Info, "conversation", $"evt=guard.come status={(walking ? "walking" : "unreachable")}");
            return walking;
        }

        /// <summary>
        /// The action a short body command asks for, when the persona is offered it: its allow-list and the character's
        /// clips decide, as for the model's calls. The pipeline asks only when no crisis keyword matched.
        /// </summary>
        public bool TryMatchAction(string transcript, out GuardedAction action)
        {
            action = null;
            string named = null;
            if ((!_actions.TryMatch(transcript, out var name, out var json)
                 && (_places == null || !_places.TryMatch(transcript, out name, out json, out named)))
                || !IsOffered(name, out var persona)
                || !_tools.TryGet(name, out var tool) || !_tools.TryValidate(name, json, out var args, out _))
                return false;
            action = new GuardedAction(name, json, tool, args, persona);
            if (name == ToolNames.RememberPlace)
                action.Reply = CoreLines.Get(_localizer, CoreLines.PlaceRemembered).Replace("{name}", named);
            return true;
        }

        /// <summary>
        /// Runs a matched action to its end (a walk: until it starts) and returns its output, or null when it did not do
        /// what was asked and the model should explain: a failed, timed-out or throwing call, or a walk that never started
        /// (unreachable, already close), as for <see cref="ApplyComeHere"/>. Cancelling fades the move out and throws.
        /// </summary>
        public async UniTask<string> RunAction(GuardedAction action, CancellationToken ct)
        {
            _events.RaiseToolStarted(action.CallId, action.Name);
            var result = await Execute(action.Tool, action.Name, action.Arguments,
                new ToolContext { Persona = action.Persona, Preferences = _preferences }, ct);
            _events.RaiseToolCompleted(action.CallId, action.Name, result.Ok);
            if (result.SpokenText != null)
                action.Reply = result.SpokenText;
            var done = result.Ok && (action.Tool.Definition.Concurrency != ToolConcurrency.Locomotion
                                     || _character.Mode == LocomotionMode.MovingTo);
            _log.Write(done ? LogLevel.Info : LogLevel.Warning, "conversation",
                       $"evt=guard.action tool={action.Name} status={(done ? "done" : "left_to_model")}");
            return done ? result.OutputJson : null;
        }

        /// <summary>
        /// A tool call under its own timeout, as in the agent's round: a stuck move would otherwise hold Acting for the 30 s
        /// watchdog and an exception would crash the turn, when the model can still explain. Only the turn's cancel throws.
        /// </summary>
        async UniTask<ToolResult> Execute(ITool tool, string name, IToolArguments args, ToolContext context, CancellationToken ct)
        {
            try
            {
                using var scope = new TimeoutScope(_clock, tool.Definition.Timeout, ct);
                try
                {
                    return await tool.Execute(args, context, scope.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested && scope.TimedOut)
                {
                    return ToolResult.Failure("timeout");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _log.Write(LogLevel.Error, "conversation", $"evt=tool.error tool={name}", e);
                return ToolResult.Failure("tool_error");
            }
        }

        /// <summary>True on a short settings command, after raising <see cref="SettingsRequested"/>; the turn then ends
        /// without the model (ADR-0042). A crisis keyword is never swallowed: the name-as-heard rule (ADR-0043) would
        /// take "Suicidal. Close settings." as a command, and this turn ends before the crisis hand-off.</summary>
        public bool ApplySettings(string transcript)
        {
            var command = _settings.Match(transcript);
            if (command == SettingsCommand.None || NeedsCrisisHandoff(transcript))
                return false;
            var open = command == SettingsCommand.Open;
            _log.Write(LogLevel.Info, "conversation", open ? "evt=guard.settings open=true" : "evt=guard.settings open=false");
            SettingsRequested?.Invoke(open);
            return true;
        }

        /// <summary>Every persona: the hand-off must never depend on which mode the person happens to be in.</summary>
        public bool NeedsCrisisHandoff(string transcript) => _crisis.Matches(transcript);

        /// <summary>
        /// "Where do these go?" with a place remembered (ADR-0051), as a capture_and_identify call: asked to choose, the
        /// model looked only 4 times in 6 (live, 2026-11-03), and otherwise answered without seeing them. Which place
        /// fits stays with the vision model.
        /// </summary>
        public bool TryMatchWhereItGoes(string transcript, out GuardedAction action)
        {
            action = null;
            const string name = ToolNames.CaptureAndIdentify;
            if (_places == null || !_places.AsksWhereItGoes(transcript) || NeedsCrisisHandoff(transcript)
                || !IsOffered(name, out var persona) || !_tools.TryGet(name, out var tool))
                return false;
            var json = new ToolJson().Add("question", transcript).ToString();
            if (!_tools.TryValidate(name, json, out var args, out _))
                return false;
            action = new GuardedAction(name, json, tool, args, persona);
            return true;
        }

        /// <summary>Runs a matched look with the turn's speech and camera frame; its answer streams into the speech.</summary>
        public async UniTask<ToolResult> RunLook(GuardedAction action, UserTurn turn, ISpokenTextSink speech, CancellationToken ct)
        {
            _events.RaiseToolStarted(action.CallId, action.Name);
            var result = await Execute(action.Tool, action.Name, action.Arguments, new ToolContext
            {
                Turn = turn, Persona = action.Persona, Preferences = _preferences, Speech = speech,
                PersonaInstructions = _personaInstructions?.Invoke(action.Persona),
            }, ct);
            _events.RaiseToolCompleted(action.CallId, action.Name, result.Ok);
            return result;
        }

        /// <summary>Only a question gets the acknowledgement (ADR-0040).</summary>
        public bool IsQuestion(string transcript) => _question.Matches(transcript);

        bool IsOffered(string name, out PersonaProfile persona)
        {
            persona = _personas.Current;
            foreach (var definition in _tools.DefinitionsFor(persona, _preferences))
            {
                if (definition.Name == name)
                    return true;
            }
            return false;
        }
    }
}
