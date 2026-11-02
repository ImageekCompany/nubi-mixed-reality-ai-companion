using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// A body action that plays gestures one after another (actions.jump, actions.wave, actions.celebrate, …; ADR-0047)
    /// and returns once the last has ended, so the model's next words come after the move. A whole-body move stops any
    /// walk first and runs Exclusive; an upper-body gesture plays alongside a walk or another call, as a wave while
    /// coming over. Logs <c>evt=action.&lt;verb&gt;</c>.
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
            Definition = new ToolDefinition(name, description, ToolSchemas.Empty,
                TimeSpan.FromSeconds(SecondsPerGesture * sequence.Length),
                wholeBody ? ToolConcurrency.Exclusive : ToolConcurrency.Parallel);
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
                BodyAction.StandStill(_character);
            foreach (var gesture in _sequence)
                await _character.PlayGesture(gesture, ct);
            _log.Write(LogLevel.Info, "tools", $"evt=action.{BodyAction.Verb(Definition.Name)} status=done");
            return ToolResult.Success("{\"status\":\"done\"}");
        }
    }

    static class BodyAction
    {
        /// <summary>"jump" for "actions.jump".</summary>
        public static string Verb(string toolName) => toolName.Substring(toolName.IndexOf('.') + 1);

        /// <summary>
        /// "Dance!" means now: a walk in progress, or following, ends where the character is instead of carrying it across
        /// the room mid-jump (ADR-0047). Waiting for the arrival instead would leave the user watching a walk first.
        /// </summary>
        public static void StandStill(ICharacterController character)
        {
            if (character.Mode != LocomotionMode.None)
                character.Stop();
        }
    }
}
