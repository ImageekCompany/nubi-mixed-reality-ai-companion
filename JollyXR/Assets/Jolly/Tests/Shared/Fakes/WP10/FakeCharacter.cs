using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.Testing.Offline
{
    /// <summary>ICharacterController with test-set locomotion state that records idle-relevant calls.
    /// Gestures complete at once; TryStartMoveTo answers <see cref="AcceptMoves"/> and does not change IsMoving.</summary>
    public sealed class FakeCharacter : ICharacterController
    {
        readonly List<GestureId> _gestures = new();
        readonly List<LookTarget> _looks = new();
        readonly List<Vector3> _moveTargets = new();

        public Vector3 Position { get; set; }
        public Vector3 Forward { get; set; } = Vector3.forward;
        public bool IsMoving { get; set; }
        public LocomotionMode Mode { get; set; }
        public bool AcceptMoves { get; set; } = true;
        public float LastStopDistance { get; private set; }

        public IReadOnlyList<GestureId> Gestures => _gestures;
        public IReadOnlyList<LookTarget> Looks => _looks;
        public IReadOnlyList<Vector3> MoveTargets => _moveTargets;
        public int ActionCount => _gestures.Count + _looks.Count + _moveTargets.Count;

        public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds)
        {
            _moveTargets.Add(worldTarget);
            LastStopDistance = stopDistance;
            etaSeconds = AcceptMoves ? 1f : 0f;
            return AcceptMoves;
        }

        public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) => UniTask.FromResult(MoveOutcome.Arrived);
        public void Follow(float distanceMin, float distanceMax) => Mode = LocomotionMode.Following;
        public void Stop() => Mode = LocomotionMode.None;
        public void Relocate(Vector3 floorPoint) => Position = floorPoint;
        public void LookAt(LookTarget target) => _looks.Add(target);

        public UniTask PlayGesture(GestureId gesture, CancellationToken ct)
        {
            _gestures.Add(gesture);
            return UniTask.CompletedTask;
        }

        public UniTask PlayLooped(GestureId gesture, float seconds, CancellationToken ct) => PlayGesture(gesture, ct);
        public bool CanPlay(GestureId gesture) => true;
        public UniTask Spin(float degrees, CancellationToken ct) => UniTask.CompletedTask;

        public void SetMood(Mood mood) { }
        public void SetConversationState(ConversationState state) { }
    }
}
