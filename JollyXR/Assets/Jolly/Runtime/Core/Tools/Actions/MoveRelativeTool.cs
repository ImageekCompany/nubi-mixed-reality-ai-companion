using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// actions.move_relative (ADR-0047): walks to a spot named relative to the user (<see cref="RelativeTarget"/>), watching
    /// them, so it ends facing them (ADR-0041). The target snaps to walkable floor within
    /// <see cref="LocomotionSettings.RelativeSampleRadius"/> and must stay <see cref="LocomotionSettings.UserMinDistance"/>
    /// from the user; otherwise it answers unreachable for the model to say so. The walk never passes through the user.
    /// Returns once the walk has started, like come_here. Logs <c>evt=action.move_relative</c>.
    /// </summary>
    public sealed class MoveRelativeTool : ITool
    {
        const float DefaultDistance = 1f;
        /// <summary>"closer" from within this much of the closer distance is already close enough, m.</summary>
        const float CloseEnoughSlack = 0.15f;
        const string Schema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"direction\":{\"type\":\"string\",\"enum\":[\"front\",\"behind\",\"left\",\"right\",\"closer\",\"away\"]," +
            "\"description\":\"Relative to the user: front = in front of them, behind = behind their back, left/right = " +
            "their left or right side, closer = right up to them, away = step back from them\"}," +
            "\"distance_m\":{\"type\":\"number\",\"minimum\":0.5,\"maximum\":3," +
            "\"description\":\"Metres from the user (front, behind, left, right) or to back off (away); default 1\"}}," +
            "\"required\":[\"direction\"],\"additionalProperties\":false}";

        readonly ICharacterController _character;
        readonly IUserPoseProvider _user;
        readonly IWorldQuery _world;
        readonly LocomotionSettings _settings;
        readonly ILog _log;

        public MoveRelativeTool(ICharacterController character, IUserPoseProvider user, IWorldQuery world,
                                LocomotionSettings settings, ILog log)
        {
            _character = character;
            _user = user;
            _world = world;
            _settings = settings;
            _log = log;
        }

        public ToolDefinition Definition { get; } = new(ToolNames.Actions.MoveRelative,
            "Go and stand somewhere relative to the user: in front of them, behind them, on their left or right, closer, " +
            "or further away. Call it whenever they ask, e.g. 'go behind me', 'stand on my left', 'back off a bit'.",
            Schema, TimeSpan.FromSeconds(3), ToolConcurrency.Locomotion);

        public UniTask<ToolResult> Execute(IToolArguments args, ToolContext context, CancellationToken ct)
        {
            args.TryGetString("direction", out var name);
            var direction = Parse(name);
            var distance = args.TryGetNumber("distance_m", out var d) ? (float)d : DefaultDistance;
            var head = _user.Head;
            var from = _character.Position;
            var output = new ToolJson().Add("direction", name);
            _character.LookAt(LookTarget.User);

            string status;
            var eta = 0f;
            if (direction == RelativeDirection.Closer
                && RelativeTarget.FlatDistance(from, head.position) <= _settings.CloserDistance + CloseEnoughSlack)
            {
                status = "already_close";
            }
            else
            {
                var target = RelativeTarget.Resolve(direction, distance, head, from, _settings);
                status = _world.TrySampleWalkable(target, _settings.RelativeSampleRadius, out var walkable)
                         && RelativeTarget.FlatDistance(walkable, head.position) >= _settings.UserMinDistance
                         && TryStartWalk(from, walkable, head.position, out eta)
                    ? "walking"
                    : "unreachable";
            }

            output.Add("status", status);
            if (status == "walking")
                output.Add("eta_s", Mathf.CeilToInt(eta));
            _log.Write(status == "unreachable" ? LogLevel.Warning : LogLevel.Info, "tools",
                $"evt=action.move_relative dir={name} dist_m={distance.ToString("0.00", CultureInfo.InvariantCulture)} status={status}");
            return UniTask.FromResult(ToolResult.Success(output.ToString()));
        }

        /// <summary>
        /// Straight to <paramref name="target"/>, unless that line passes within <see cref="LocomotionSettings.UserPassClearance"/>
        /// of the user ("behind me" from in front of them would walk through them): then one motion by way of a point
        /// beside the user (<see cref="RelativeTarget.Detour"/>, the character's side first). A turn is used only if both
        /// legs keep the clearance; a character already standing nearer than that is let walk out to the turn. The
        /// locomotion checks the second leg again against where the user is when it starts.
        /// </summary>
        bool TryStartWalk(Vector3 from, Vector3 target, Vector3 user, out float eta)
        {
            var clearance = _settings.UserPassClearance;
            if (RelativeTarget.PassDistance(from, target, user) >= clearance)
                return _character.TryStartMoveTo(target, _settings.ComeHereStopDistance, out eta);
            var firstLegClearance = RelativeTarget.FlatDistance(from, user) >= clearance ? clearance : 0f;
            foreach (var otherSide in new[] { false, true })
            {
                var turn = RelativeTarget.Detour(from, target, user, _settings.DetourRadius, otherSide);
                if (_world.TrySampleWalkable(turn, _settings.RelativeSampleRadius, out var walkable)
                    && RelativeTarget.PassDistance(from, walkable, user) >= firstLegClearance
                    && RelativeTarget.PassDistance(walkable, target, user) >= clearance
                    && _character.TryStartMoveVia(walkable, target, _settings.ComeHereStopDistance,
                                                  _settings.UserMinDistance, out eta))
                    return true;
            }
            eta = 0f;
            return false;
        }

        /// <summary>The schema enum guarantees one of these names.</summary>
        static RelativeDirection Parse(string name) => name switch
        {
            "front" => RelativeDirection.Front,
            "behind" => RelativeDirection.Behind,
            "left" => RelativeDirection.Left,
            "right" => RelativeDirection.Right,
            "closer" => RelativeDirection.Closer,
            _ => RelativeDirection.Away,
        };
    }
}
