using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Tools;
using UnityEngine;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// The guard path of a short walk (ADR-0047): "Go stand behind me." must run actions.move_relative with no model round
    /// and the character must end up behind where the
    /// user faced when they spoke: at least <see cref="MinBehindDegrees"/> off their forward, within reach. It needs free
    /// floor behind the headset; with no walkable area at all it is degraded, like voice_come_here.
    /// </summary>
    public static class ActionMoveBehindScenario
    {
        public const string Name = "action_move_behind";
        public const float MinBehindDegrees = 135f;
        const float MinMovedMeters = 0.3f;
        const float MaxDistanceMeters = 1.8f;
        static readonly TimeSpan ArrivalWait = TimeSpan.FromSeconds(10);

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var nubi = c.Graph.Scene.nubi;
            var head = c.Graph.Xr.User.Head;
            var from = nubi.Position;
            var r = await VoiceTurnScenario.Run(c, Name, c.Assets.moveBehind, ToolNames.Actions.MoveRelative, ct, checkFacing: false);
            TurnChecks.GuardPath(r, TurnChecks.ActionSource(c.Observer.ToolCallIds, c.Observer.Replies.Count));
            var outcome = await nubi.WhenArrived(ct).TimeoutWithoutException(ArrivalWait);
            var at = nubi.Position;
            var moved = RelativeTarget.FlatDistance(from, at);
            var bearing = Bearing(head, at);
            var distance = RelativeTarget.FlatDistance(head.position, at);
            var area = c.Graph.Xr.World.WalkableArea;
            r.Metric("navmesh", area.ToString().ToLowerInvariant())
             .Metric("walk", outcome.IsTimeout ? "still_walking" : outcome.Result.ToString())
             .Metric("moved_m", moved).Metric("bearing_deg", bearing).Metric("user_distance_m", distance);
            if (area == WalkableArea.None)
            {
                r.Degrade("no walkable floor: the tool answered without moving");
                return r;
            }
            if (r.Check(!outcome.IsTimeout && moved >= MinMovedMeters, "the character did not walk"))
                r.Check(bearing >= MinBehindDegrees && distance <= MaxDistanceMeters, "the character did not end up behind the user");
            return r;
        }

        /// <summary>Unsigned degrees between the user's flat forward and the direction to <paramref name="point"/>.</summary>
        static float Bearing(Pose head, Vector3 point)
        {
            var forward = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
            var to = Vector3.ProjectOnPlane(point - head.position, Vector3.up);
            return Vector3.Angle(forward, to);
        }
    }
}
