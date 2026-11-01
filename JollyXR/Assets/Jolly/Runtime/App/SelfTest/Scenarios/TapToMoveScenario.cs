using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using Jolly.Core.Spatial;
using UnityEngine;

namespace Jolly.App.SelfTest.Scenarios
{
    /// <summary>
    /// ADR-0044: a quick pinch with the ray on the floor sends the character there and never talks. The scripted pointer
    /// aims from the head at walkable floor about 1.5 m from the character, and a 100 ms pinch goes through the real
    /// tap/hold split, target resolution (the scene raycast and the NavMesh) and walk. It must arrive
    /// (<c>walk=Arrived</c>) near the point with no conversation state change (so no ASR session, no model request,
    /// no "Listening…") and no spend. The microphone pre-roll must have started at the pinch and been dropped unheard.
    /// </summary>
    public static class TapToMoveScenario
    {
        public const string Name = "tap_to_move";
        const float TargetDistance = 1.5f;
        const float UserRadius = 1f;
        const float SampleRadius = 0.2f;
        const float MaxArrivalError = 0.35f;
        const double RoomSettleSeconds = 15;
        static readonly TimeSpan TapHold = TimeSpan.FromSeconds(0.1);
        static readonly TimeSpan ArrivalWait = TimeSpan.FromSeconds(10);

        public static async UniTask<ScenarioResult> Run(SelfTestContext c, CancellationToken ct)
        {
            var r = new ScenarioResult(Name);
            var graph = c.Graph;
            await c.WaitUntil(() => graph.Room.Status != RoomScanStatus.Pending, RoomSettleSeconds, ct);
            await c.WaitForIdle(20, ct);
            var nubi = graph.Scene.nubi;
            var world = graph.Xr.World;
            var head = graph.Xr.User.Head.position;
            r.Metric("navmesh", world.WalkableArea.ToString().ToLowerInvariant());
            if (world.WalkableArea == WalkableArea.None)
            {
                r.Degrade("no walkable floor (no Space Setup and no fallback floor): nothing to tap on");
                return r;
            }
            if (!r.Check(TryPickPoint(world, head, nubi.Position, out var point), "no walkable floor point near the user"))
                return r;

            c.Observer.Reset();
            var usage = graph.Foundation.Usage;
            var spentBefore = usage.SpentUsd;
            var micStarts = c.Session.Microphone.StartCount;
            var from = nubi.Position;
            MoveTarget? moved = null;
            PointerSource source = default;
            void OnMoved(MoveTarget target, PointerSource by)
            {
                moved = target;
                source = by;
            }

            graph.Point.Move.Moved += OnMoved;
            try
            {
                c.Session.Pointer.Aim = new Ray(head, point - head);
                c.Session.Voice.Press();
                await c.Clock.Delay(TapHold, ct);
                c.Session.Voice.Release();
            }
            finally
            {
                c.Session.Pointer.Aim = null;
                graph.Point.Move.Moved -= OnMoved;
            }

            r.Metric("dist_m", Flat(point - from)).Metric("source", moved.HasValue ? source.ToString().ToLowerInvariant() : "none")
             .Metric("mic_preroll", c.Session.Microphone.StartCount > micStarts ? "started" : "none");
            if (!r.Check(moved.HasValue, "the tap started no walk"))
                return r;
            var outcome = await nubi.WhenArrived(ct).TimeoutWithoutException(ArrivalWait);
            var error = Flat(nubi.Position - point);
            r.Metric("walk", outcome.IsTimeout ? "still_walking" : outcome.Result.ToString()).Metric("error_m", error)
             .Metric("states", c.Observer.StatePath()).Metric("spent_usd", (double)(usage.SpentUsd - spentBefore));
            r.Check(!outcome.IsTimeout && outcome.Result == MoveOutcome.Arrived, "the character did not arrive");
            r.Check(error <= MaxArrivalError, "arrived farther than 0.35 m from the tapped point");
            r.Check(c.Observer.States.Count == 0 && c.Observer.UserFinal == null,
                "the tap reached the conversation (a state change or a transcript)");
            r.Check(usage.SpentUsd == spentBefore, "the tap spent money");
            if (c.Session.Microphone.StartCount == micStarts)
                r.Degrade("the microphone pre-roll did not start at the pinch");
            return r;
        }

        /// <summary>Walkable floor <see cref="UserRadius"/> from the user whose distance to the character is nearest
        /// <see cref="TargetDistance"/>: inside the fallback disc too, which is 1.5 m around the user.</summary>
        static bool TryPickPoint(IWorldQuery world, Vector3 head, Vector3 character, out Vector3 point)
        {
            point = default;
            var best = float.PositiveInfinity;
            for (var degrees = 0; degrees < 360; degrees += 30)
            {
                var direction = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
                var candidate = new Vector3(head.x, character.y, head.z) + direction * UserRadius;
                if (!world.TrySampleWalkable(candidate, SampleRadius, out var walkable))
                    continue;
                var miss = Mathf.Abs(Flat(walkable - character) - TargetDistance);
                if (miss >= best)
                    continue;
                best = miss;
                point = walkable;
            }
            return !float.IsPositiveInfinity(best);
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
