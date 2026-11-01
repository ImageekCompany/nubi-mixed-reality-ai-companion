using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;

namespace Jolly.Core.Conversation
{
    /// <summary>The local, model-free reactions to a final transcript (architecture §5.1, §9).</summary>
    public sealed class TurnGuards
    {
        /// <summary>Appended to the user text after the hand-off line has been spoken, so the reply follows it up.</summary>
        public const string CrisisNote =
            "\n\n(Note: emergency and crisis-line contacts were just read out to the user. Respond with warmth, take them " +
            "seriously, and encourage them to reach out to those services or someone they trust now.)";

        /// <summary>
        /// Appended once the guard has started the walk. A come_here call the model makes anyway only restarts the same
        /// walk, so the note keeps the reply short rather than preventing a conflict.
        /// </summary>
        public const string ComeHereNote =
            "\n\n(Note: you have already started walking to the user. Reply briefly in words and do not call come_here again.)";

        /// <summary>The call id of the guard's come_here on <see cref="ConversationEvents.ToolStarted"/>; the model's
        /// calls carry the provider's ids.</summary>
        public const string ComeHereCallId = "guard_come_here";

        readonly StopGuard _stop;
        readonly ComeGuard _come;
        readonly CrisisGuard _crisis;
        readonly ICharacterController _character;
        readonly IToolRegistry _tools;
        readonly IPersonaProvider _personas;
        readonly IUserPreferences _preferences;
        readonly ConversationEvents _events;
        readonly ILog _log;

        public TurnGuards(StopGuard stop, ComeGuard come, CrisisGuard crisis, ICharacterController character,
                          IToolRegistry tools, IPersonaProvider personas, IUserPreferences preferences,
                          ConversationEvents events, ILog log)
        {
            _stop = stop;
            _come = come;
            _crisis = crisis;
            _character = character;
            _tools = tools;
            _personas = personas;
            _preferences = preferences;
            _events = events;
            _log = log;
        }

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
        /// promise, as for the model's calls). True when Nubi is walking; the turn still goes to the model, with
        /// <see cref="ComeHereNote"/>. An unreachable target returns false, so the model's own call explains it.
        /// </summary>
        public async UniTask<bool> ApplyComeHere(string transcript, CancellationToken ct)
        {
            if (!_come.Matches(transcript) || !IsOffered(ToolNames.ComeHere, out var persona)
                || !_tools.TryGet(ToolNames.ComeHere, out var tool)
                || !_tools.TryValidate(ToolNames.ComeHere, "{}", out var args, out _))
                return false;

            _events.RaiseToolStarted(ComeHereCallId, ToolNames.ComeHere);
            var result = await tool.Execute(args, new ToolContext { Persona = persona, Preferences = _preferences }, ct);
            _events.RaiseToolCompleted(ComeHereCallId, ToolNames.ComeHere, result.Ok);
            var walking = result.Ok && _character.Mode == LocomotionMode.MovingTo;
            _log.Write(LogLevel.Info, "conversation", $"evt=guard.come status={(walking ? "walking" : "unreachable")}");
            return walking;
        }

        /// <summary>Every persona: the hand-off must never depend on which mode the person happens to be in.</summary>
        public bool NeedsCrisisHandoff(string transcript) => _crisis.Matches(transcript);

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
