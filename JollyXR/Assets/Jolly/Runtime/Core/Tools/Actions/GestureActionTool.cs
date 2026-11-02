using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// A body action that plays gestures one after another (actions.jump, actions.wave, actions.celebrate, …; ADR-0047)
    /// and returns once the last has ended, so the model's next words come after the move. A whole-body move runs
    /// Exclusive and never while walking (<see cref="BodyAction"/>); an upper-body gesture plays alongside a walk or
    /// another call, as a wave while coming over. Logs <c>evt=action.&lt;verb&gt;</c>.
    /// </summary>
    public sealed class GestureActionTool : ITool
    {
        const double SecondsPerGesture = 4;

        readonly ICharacterController _character;
        readonly ILog _log;
        readonly GestureId[] _sequence;
        readonly bool _wholeBody;

        /// <param name="wholeBody">The move uses the legs (a jump, a bow): the character stands still for it.</param>
        public GestureActionTool(string name, string description, ICharacterController character, ILog log, bool wholeBody,
                                 params GestureId[] sequence)
        {
            _character = character;
            _log = log;
            _sequence = sequence;
            _wholeBody = wholeBody;
            var seconds = TimeSpan.FromSeconds(SecondsPerGesture * sequence.Length);
            Definition = wholeBody
                ? new ToolDefinition(name, description, ToolSchemas.Empty, seconds + BodyAction.WalkWait, ToolConcurrency.Exclusive)
                : new ToolDefinition(name, description, ToolSchemas.Empty, seconds, ToolConcurrency.Parallel);
        }

        public ToolDefinition Definition { get; }

        /// <summary>Whether the character has a clip for every gesture of the sequence.</summary>
        public bool CanPlay()
        {
            foreach (var gesture in _sequence)
            {
                if (!_character.CanPlay(gesture))
                    return false;
            }
            return true;
        }

        public async UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            if (_wholeBody)
                await BodyAction.Settle(_character, ct);
            foreach (var gesture in _sequence)
            {
                await _character.PlayGesture(gesture, ct);
                if (_wholeBody && BodyAction.CutShort(_character))
                    return BodyAction.Interrupted(_log, Definition.Name, "");
            }
            return _wholeBody
                ? BodyAction.Done(_log, Definition.Name, "")
                : BodyAction.Played(_log, Definition.Name);
        }
    }

    /// <summary>
    /// A whole-body action (jump, bow, celebrate, dance, spin) and walking never overlap (ADR-0047): played while the
    /// agent moves, the clip slides the character across the floor. The action lets a walk to a spot finish first, so
    /// "come here and dance" and "go behind me, then jump" do both, and ends following, which never arrives. A walk
    /// that starts during the action ends it (<see cref="ICharacterController.TryStartMoveTo"/>), and the action then
    /// reports interrupted_by_walk instead of done.
    /// </summary>
    static class BodyAction
    {
        /// <summary>The longest wait for a walk to arrive, added to a whole-body action's timeout.</summary>
        public static readonly TimeSpan WalkWait = TimeSpan.FromSeconds(10);

        /// <summary>"jump" for "actions.jump".</summary>
        public static string Verb(string toolName) => toolName.Substring(toolName.IndexOf('.') + 1);

        /// <summary>Returns once the character stands still. A walk superseded meanwhile is waited for in turn.</summary>
        public static async UniTask Settle(ICharacterController character, CancellationToken ct)
        {
            while (character.Mode != LocomotionMode.None)
            {
                if (character.Mode == LocomotionMode.Following)
                    character.Stop();
                else
                    await character.WhenArrived(ct);
            }
        }

        /// <summary>After the move: a walk running now started during it and ended it.</summary>
        public static bool CutShort(ICharacterController character) => character.Mode != LocomotionMode.None;

        /// <summary>A whole-body move played out: it is the answer, so a round that already spoke needs no closing words
        /// after it, and the turn ends when the move ends (the closing round took 1.5-2 s on the headset, 2026-11-02).</summary>
        /// <param name="detail">Log fields before the status, each with a trailing space.</param>
        public static ToolResult Done(ILog log, string toolName, string detail)
        {
            Log(log, toolName, detail, "done");
            return ToolResult.Success(Output("done"), endsSpokenTurn: true);
        }

        /// <summary>An upper-body gesture played out: punctuation, so the model may still have something to say.</summary>
        public static ToolResult Played(ILog log, string toolName)
        {
            Log(log, toolName, "", "done");
            return ToolResult.Success(Output("done"));
        }

        /// <inheritdoc cref="Done"/>
        public static ToolResult Interrupted(ILog log, string toolName, string detail)
        {
            Log(log, toolName, detail, "interrupted_by_walk");
            return ToolResult.Success(Output("interrupted_by_walk"));
        }

        static void Log(ILog log, string toolName, string detail, string status) =>
            log.Write(LogLevel.Info, "tools", $"evt=action.{Verb(toolName)} {detail}status={status}");

        static string Output(string status) => "{\"status\":\"" + status + "\"}";
    }
}
