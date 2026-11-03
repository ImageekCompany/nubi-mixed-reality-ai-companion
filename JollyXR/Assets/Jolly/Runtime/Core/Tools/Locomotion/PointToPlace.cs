using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Config;
using UnityEngine;

namespace Jolly.Core.Tools
{
    /// <summary>
    /// "There!" (ADR-0051): walks as far toward a world point as the floor allows, stopping short of it, then turns so the
    /// Point clip's arm aims at it and starts the clip. A place in another room is usually past the scanned floor, so a
    /// walk that cannot reach it takes the furthest reachable spot on the way, and no reachable spot still ends in a point.
    /// The clip is left playing on return, so the spoken "There!" lands while the arm is out. Only a stop or a newer move
    /// during the walk skips the point; the walk itself runs on the controller's token and survives barge-in.
    /// </summary>
    public sealed class PointToPlace
    {
        /// <summary>Added to a tool's timeout for the walk.</summary>
        public static readonly TimeSpan WalkBudget = TimeSpan.FromSeconds(20);

        const int WaySteps = 8;

        readonly ICharacterController _character;
        readonly IWorldQuery _world;
        readonly LocomotionSettings _settings;
        readonly ILog _log;

        public PointToPlace(ICharacterController character, IWorldQuery world, LocomotionSettings settings, ILog log)
        {
            _character = character;
            _world = world;
            _settings = settings;
            _log = log;
        }

        /// <returns>False when a stop or a newer move ended the walk, so nothing was pointed at.</returns>
        public async UniTask<bool> Run(Vector3 target, CancellationToken ct)
        {
            _character.LookAt(LookTarget.At(target));
            var way = TryWalkToward(target);
            if (way > 0f)
            {
                if (await _character.WhenArrived(ct) == MoveOutcome.Cancelled)
                {
                    _log.Write(LogLevel.Info, "tools", "evt=place.point status=stopped");
                    return false;
                }
            }
            else
            {
                await BodyAction.Settle(_character, ct);
            }

            var turn = TurnToAim(target);
            if (Mathf.Abs(turn) >= _settings.PointMinTurn)
                await _character.Spin(turn, ct);
            // A spin ends in attending the user; a fresh point holds the body and head here for the clip.
            _character.LookAt(LookTarget.At(target));
            if (_character.CanPlay(GestureId.Point))
                _character.PlayGesture(GestureId.Point, CancellationToken.None).Forget();
            _log.Write(LogLevel.Info, "tools",
                FormattableString.Invariant($"evt=place.point status=pointing way={way:F2} turn={turn:F0}"));
            return true;
        }

        /// <summary>The fraction of the way to the stand-off spot that was started, 0 when no walk was.</summary>
        float TryWalkToward(Vector3 target)
        {
            var from = _character.Position;
            var toTarget = Vector3.ProjectOnPlane(target - from, Vector3.up);
            var walk = toTarget.magnitude - _settings.ShowStandOff;
            if (walk < _settings.ShowMinWalk)
                return 0f;
            for (var step = WaySteps; step > 0; step--)
            {
                var fraction = step / (float)WaySteps;
                var spot = from + toTarget.normalized * (walk * fraction);
                if (_world.TrySampleWalkable(spot, _settings.RelativeSampleRadius, out var walkable)
                    && _character.TryStartMoveTo(walkable, _settings.SurfaceStopDistance, out _))
                    return fraction;
            }
            return 0f;
        }

        /// <summary>Degrees, clockwise from above, that put the arm rather than the belly on the target.</summary>
        float TurnToAim(Vector3 target)
        {
            var toTarget = Vector3.ProjectOnPlane(target - _character.Position, Vector3.up);
            var distance = toTarget.magnitude;
            if (distance < 0.01f)
                return 0f;
            // The arm's line starts at the shoulder: the target must sit further right of the body the nearer it is.
            var armYaw = _settings.PointArmYaw * Mathf.Deg2Rad;
            var parallax = Mathf.Asin(Mathf.Clamp01(_settings.PointShoulderOffset * Mathf.Cos(armYaw) / distance)) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, Vector3.SignedAngle(_character.Forward, toTarget, Vector3.up) - _settings.PointArmYaw - parallax);
        }
    }
}
