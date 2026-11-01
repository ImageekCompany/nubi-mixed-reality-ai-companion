using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace Jolly.Character
{
    /// <summary>
    /// NavMeshAgent locomotion behind <see cref="ICharacterController"/>'s move/follow/stop contract (contracts §7).
    /// Each command owns one completion source; a newer command or <see cref="Stop"/> completes the previous one with
    /// Cancelled. Awaiters never own the motion: <see cref="WhenArrived"/>'s token only detaches the awaiter, so a
    /// barge-in cannot halt a walk. Polled from the owner's Update; no per-frame allocation.
    /// </summary>
    public sealed class AgentLocomotion
    {
        const float ArrivalSlack = 0.05f;
        const float FollowStopDistance = 0.1f;

        readonly NavMeshAgent _agent;
        readonly IClock _clock;
        readonly LocomotionSettings _s;
        readonly NavMeshPath _path = new();
        readonly Vector3[] _corners = new Vector3[64];
        UniTaskCompletionSource<MoveOutcome> _command = Completed(MoveOutcome.Arrived);
        double _deadline;
        float _followMin;
        float _followMax;
        double _nextRepath;
        Vector3 _followGoal;
        bool _hasFollowGoal;

        public AgentLocomotion(NavMeshAgent agent, IClock clock, LocomotionSettings settings)
        {
            _agent = agent;
            _clock = clock;
            _s = settings;
            _agent.updateRotation = false;
            _agent.acceleration = settings.acceleration;
        }

        public LocomotionMode Mode { get; private set; }

        public bool IsMoving => Mode != LocomotionMode.None && OnNavMesh && (_agent.pathPending || _agent.hasPath);

        public Vector3 Velocity => OnNavMesh ? _agent.velocity : Vector3.zero;

        bool OnNavMesh => _agent.enabled && _agent.isOnNavMesh;

        public bool TryStartMoveTo(Vector3 target, float stopDistance, out float etaSeconds)
        {
            Halt();
            etaSeconds = 0f;
            if (!TryPlan(target, out var length))
            {
                Supersede(LocomotionMode.None, Completed(MoveOutcome.Unreachable));
                return false;
            }

            _agent.speed = _s.walkSpeed;
            _agent.stoppingDistance = stopDistance;
            _agent.SetPath(_path);
            etaSeconds = length / _s.walkSpeed;
            _deadline = _clock.Now + 2f * etaSeconds + _s.timeoutSlack;
            Supersede(LocomotionMode.MovingTo, new UniTaskCompletionSource<MoveOutcome>());
            return true;
        }

        public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) =>
            ct.CanBeCanceled ? _command.Task.AttachExternalCancellation(ct) : _command.Task;

        public void Follow(float distanceMin, float distanceMax)
        {
            Halt();
            _followMin = distanceMin;
            _followMax = distanceMax;
            _nextRepath = 0d;
            _hasFollowGoal = false;
            Supersede(LocomotionMode.Following, new UniTaskCompletionSource<MoveOutcome>());
        }

        public void Stop() => Finish(MoveOutcome.Cancelled);

        /// <summary>Puts the agent at point without walking. A running move ends Cancelled, since its path started
        /// elsewhere; follow mode re-plans at once. With no NavMesh at point yet, the agent waits disabled there and
        /// <see cref="EnsureOnNavMesh"/> attaches it on the next plan.</summary>
        public void Relocate(Vector3 point)
        {
            if (Mode == LocomotionMode.MovingTo)
                Finish(MoveOutcome.Cancelled);
            Halt();
            if (!OnNavMesh || !_agent.Warp(point))
            {
                _agent.enabled = false;
                _agent.transform.position = point;
            }
            _hasFollowGoal = false;
            _nextRepath = 0d;
        }

        public void Tick(Vector3 userPosition)
        {
            if (Mode == LocomotionMode.MovingTo)
                TickMove();
            else if (Mode == LocomotionMode.Following)
                TickFollow(userPosition);
        }

        void TickMove()
        {
            if (!OnNavMesh)
                Finish(MoveOutcome.Unreachable);
            else if (_agent.pathPending)
                return;
            else if (_agent.pathStatus == NavMeshPathStatus.PathInvalid)
                Finish(MoveOutcome.Unreachable);
            else if (_agent.remainingDistance <= _agent.stoppingDistance + ArrivalSlack)
                Finish(MoveOutcome.Arrived);
            else if (_clock.Now > _deadline)
                Finish(MoveOutcome.TimedOut);
        }

        void TickFollow(Vector3 user)
        {
            var now = _clock.Now;
            if (now < _nextRepath)
                return;
            _nextRepath = now + _s.followRepathInterval;

            var self = _agent.transform.position;
            if (!FollowPlanner.TryGetGoal(self, user, _followMin, _followMax, out var goal))
            {
                Halt();
                _hasFollowGoal = false;
                return;
            }
            if (_hasFollowGoal && _agent.hasPath && (goal - _followGoal).sqrMagnitude < _s.followRepathDistance * _s.followRepathDistance)
                return;
            if (!TryPlan(goal, out _))
                return;

            var far = Vector3.ProjectOnPlane(user - self, Vector3.up).sqrMagnitude > _s.catchUpDistance * _s.catchUpDistance;
            _agent.speed = far ? _s.catchUpSpeed : _s.walkSpeed;
            _agent.stoppingDistance = FollowStopDistance;
            _agent.SetPath(_path);
            _followGoal = goal;
            _hasFollowGoal = true;
        }

        /// <summary>State first, then the result: awaiters continue synchronously inside TrySetResult and may issue
        /// the next command, which must not be overwritten afterwards.</summary>
        void Finish(MoveOutcome outcome)
        {
            Mode = LocomotionMode.None;
            Halt();
            _command.TrySetResult(outcome);
        }

        /// <inheritdoc cref="Finish"/>
        void Supersede(LocomotionMode mode, UniTaskCompletionSource<MoveOutcome> next)
        {
            var previous = _command;
            _command = next;
            Mode = mode;
            previous.TrySetResult(MoveOutcome.Cancelled);
        }

        void Halt()
        {
            if (!OnNavMesh)
                return;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        bool TryPlan(Vector3 target, out float length)
        {
            length = 0f;
            if (!EnsureOnNavMesh())
                return false;
            if (!NavMesh.SamplePosition(target, out var hit, _s.sampleRadius, _agent.areaMask))
                return false;
            if (!NavMesh.CalculatePath(_agent.transform.position, hit.position, _agent.areaMask, _path)
                || _path.status != NavMeshPathStatus.PathComplete)
                return false;

            var count = _path.GetCornersNonAlloc(_corners);
            for (var i = 1; i < count; i++)
                length += Vector3.Distance(_corners[i - 1], _corners[i]);
            return true;
        }

        /// <summary>The prefab ships the agent disabled: enabling it off a NavMesh logs an error every time, and the
        /// floor NavMesh only exists once scene permission is granted and the floor plane is found.</summary>
        bool EnsureOnNavMesh()
        {
            if (OnNavMesh)
                return true;
            if (!NavMesh.SamplePosition(_agent.transform.position, out var hit, _s.sampleRadius, _agent.areaMask))
                return false;
            _agent.enabled = true;
            return _agent.Warp(hit.position) && _agent.isOnNavMesh;
        }

        static UniTaskCompletionSource<MoveOutcome> Completed(MoveOutcome outcome)
        {
            var source = new UniTaskCompletionSource<MoveOutcome>();
            source.TrySetResult(outcome);
            return source;
        }
    }
}
