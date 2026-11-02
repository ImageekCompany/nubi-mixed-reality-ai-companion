using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>ICharacterController that records every command. Moves succeed unless <see cref="Reachable"/> is false, and
    /// a started move arrives when it is awaited.</summary>
    public sealed class RecordingCharacterController : ICharacterController
    {
        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; } = Vector3.forward;
        public bool IsMoving => Mode == LocomotionMode.MovingTo;
        public LocomotionMode Mode { get; private set; }

        public bool Reachable { get; set; } = true;
        public float Eta { get; set; } = 3.2f;
        public Vector3? MoveTarget { get; private set; }
        /// <summary>Every started move's target, in order.</summary>
        public List<Vector3> MoveTargets { get; } = new();
        public float MoveStopDistance { get; private set; }
        /// <summary>The last move's waypoint (<see cref="TryStartMoveVia"/>); null after a straight move.</summary>
        public Vector3? Waypoint { get; private set; }
        public float MinUserDistance { get; private set; }
        /// <summary>Runs while a gesture, loop or spin plays, e.g. to start a walk in the middle of one.</summary>
        public Action WhilePlaying { get; set; }
        /// <summary>When set, a gesture, loop or spin takes as long as the task this returns, for a move still running.</summary>
        public Func<CancellationToken, UniTask> Playing { get; set; }
        public (float min, float max)? FollowRange { get; private set; }
        public int StopCount { get; private set; }
        public List<Vector3> Relocations { get; } = new();
        public LookTarget? LastLook { get; private set; }
        public List<GestureId> Gestures { get; } = new();
        /// <summary>The token each <see cref="PlayGesture"/> was given, in order.</summary>
        public List<CancellationToken> GestureTokens { get; } = new();
        /// <summary>Gestures <see cref="CanPlay"/> denies; every gesture is playable by default.</summary>
        public HashSet<GestureId> Missing { get; } = new();
        public List<(GestureId gesture, float seconds)> Looped { get; } = new();
        public List<float> Spins { get; } = new();
        public Mood? LastMood { get; private set; }
        public List<ConversationState> States { get; } = new();

        public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds)
        {
            etaSeconds = Reachable ? Eta : 0f;
            if (!Reachable)
                return false;
            MoveTarget = worldTarget;
            MoveTargets.Add(worldTarget);
            MoveStopDistance = stopDistance;
            Waypoint = null;
            Mode = LocomotionMode.MovingTo;
            return true;
        }

        public bool TryStartMoveVia(Vector3 waypoint, Vector3 worldTarget, float stopDistance, float minUserDistance,
                                    out float etaSeconds)
        {
            if (!TryStartMoveTo(worldTarget, stopDistance, out etaSeconds))
                return false;
            Waypoint = waypoint;
            MinUserDistance = minUserDistance;
            return true;
        }

        /// <summary>A move in progress arrives at once.</summary>
        public UniTask<MoveOutcome> WhenArrived(CancellationToken ct)
        {
            if (Mode == LocomotionMode.MovingTo)
                Mode = LocomotionMode.None;
            return UniTask.FromResult(MoveOutcome.Arrived);
        }

        public void Follow(float distanceMin, float distanceMax)
        {
            FollowRange = (distanceMin, distanceMax);
            Mode = LocomotionMode.Following;
        }

        public void Stop()
        {
            StopCount++;
            Mode = LocomotionMode.None;
        }

        /// <summary>Records the target and moves there at once (no pop).</summary>
        public void Relocate(Vector3 floorPoint)
        {
            Relocations.Add(floorPoint);
            Position = floorPoint;
        }

        public void LookAt(LookTarget target) => LastLook = target;

        public UniTask PlayGesture(GestureId gesture, CancellationToken ct)
        {
            Gestures.Add(gesture);
            GestureTokens.Add(ct);
            WhilePlaying?.Invoke();
            return Playing?.Invoke(ct) ?? UniTask.CompletedTask;
        }

        public UniTask PlayLooped(GestureId gesture, float seconds, CancellationToken ct)
        {
            Looped.Add((gesture, seconds));
            WhilePlaying?.Invoke();
            return Playing?.Invoke(ct) ?? UniTask.CompletedTask;
        }

        public bool CanPlay(GestureId gesture) => !Missing.Contains(gesture);

        public UniTask Spin(float degrees, CancellationToken ct)
        {
            Spins.Add(degrees);
            WhilePlaying?.Invoke();
            return Playing?.Invoke(ct) ?? UniTask.CompletedTask;
        }

        public void SetMood(Mood mood) => LastMood = mood;
        public void SetConversationState(ConversationState state) => States.Add(state);
    }
}
