using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Conversation
{
    /// <summary>ICharacterController that records every command. Moves succeed unless <see cref="Reachable"/> is false.</summary>
    public sealed class RecordingCharacterController : ICharacterController
    {
        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; } = Vector3.forward;
        public bool IsMoving => Mode == LocomotionMode.MovingTo;
        public LocomotionMode Mode { get; private set; }

        public bool Reachable { get; set; } = true;
        public float Eta { get; set; } = 3.2f;
        public Vector3? MoveTarget { get; private set; }
        public float MoveStopDistance { get; private set; }
        public (float min, float max)? FollowRange { get; private set; }
        public int StopCount { get; private set; }
        public LookTarget? LastLook { get; private set; }
        public List<GestureId> Gestures { get; } = new();
        public Mood? LastMood { get; private set; }
        public List<ConversationState> States { get; } = new();

        public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds)
        {
            etaSeconds = Reachable ? Eta : 0f;
            if (!Reachable)
                return false;
            MoveTarget = worldTarget;
            MoveStopDistance = stopDistance;
            Mode = LocomotionMode.MovingTo;
            return true;
        }

        public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) => UniTask.FromResult(MoveOutcome.Arrived);

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

        public void LookAt(LookTarget target) => LastLook = target;

        public UniTask PlayGesture(GestureId gesture, CancellationToken ct)
        {
            Gestures.Add(gesture);
            return UniTask.CompletedTask;
        }

        public void SetMood(Mood mood) => LastMood = mood;
        public void SetConversationState(ConversationState state) => States.Add(state);
    }
}
