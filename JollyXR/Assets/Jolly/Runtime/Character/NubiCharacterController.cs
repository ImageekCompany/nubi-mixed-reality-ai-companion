using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.AI;

namespace Jolly.Character
{
    /// <summary>
    /// Nubi's <see cref="ICharacterController"/>: a thin MonoBehaviour shell that orders the plain-C# parts per frame.
    /// Update: locomotion → body yaw → Animator parameters. The Animator then evaluates, and LateUpdate layers the
    /// look-at on the animated bones, writes the face (clip preset/mood → lip-sync → blink) and shapes the contact
    /// shadow from the animated feet.
    /// Inert until <see cref="Construct"/> is called by the composition root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NubiCharacterController : MonoBehaviour, ICharacterController
    {
        static readonly float[] LookAroundYaws = { -40f, 40f, 0f };
        static readonly TimeSpan LookAroundHold = TimeSpan.FromSeconds(0.8);

        [SerializeField] Animator _animator;
        [SerializeField] NavMeshAgent _agent;
        [SerializeField] SkinnedMeshRenderer _faceMesh;
        [SerializeField] TextAsset _facePresets;
        [SerializeField] NubiRigBones _bones = new();
        [SerializeField] ContactShadowRig _shadowRig = new();
        [SerializeField] NubiCharacterSettings _settings = new();

        IUserPoseProvider _user;
        ILipSyncDriver _lipSync;
        IClock _clock;
        AgentLocomotion _locomotion;
        CharacterAnimator _parameters;
        GestureDriver _gestures;
        LookAtDriver _look;
        ClipExpressionSelector _expression;
        MouthSmoother _mouth;
        BlinkScheduler _blink;
        FaceMixer _mixer;
        FaceRenderer _faceRenderer;
        ContactShadow _shadow;
        LookTarget _lookTarget = LookTarget.User;
        Mood _mood;
        ConversationState _conversation;
        bool _hasLookOverride;
        Vector3 _lookOverride;
        int _lookAroundGeneration;
        float _speed;

        public bool IsConstructed => _locomotion != null;
        public NubiCharacterSettings Settings => _settings;
        public Animator Animator => _animator;
        public SkinnedMeshRenderer FaceMesh => _faceMesh;
        public ContactShadowRig ShadowRig => _shadowRig;

        public void Construct(IUserPoseProvider user, ILipSyncDriver lipSync, IClock clock, IRandom random)
        {
            if (IsConstructed)
                throw new InvalidOperationException("NubiCharacterController is already constructed.");
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _lipSync = lipSync ?? throw new ArgumentNullException(nameof(lipSync));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (_animator == null || _agent == null || _faceMesh == null || _facePresets == null || !_bones.IsComplete
                || !_shadowRig.IsComplete)
                throw new InvalidOperationException("Nubi prefab references are not bound; rebuild it with Jolly/Character/Rebuild Nubi Assets.");

            var presets = FacePresetTable.Parse(_facePresets.text);
            _parameters = new CharacterAnimator(_animator, _settings.locomotion);
            _gestures = new GestureDriver(_animator);
            _look = new LookAtDriver(_bones, transform, _settings.look);
            _expression = new ClipExpressionSelector(_animator, presets, _settings.locomotion);
            _mouth = new MouthSmoother(_settings.face);
            _blink = new BlinkScheduler(random, _settings.face);
            _mixer = new FaceMixer(_settings.face);
            _faceRenderer = new FaceRenderer(_faceMesh);
            _shadow = new ContactShadow(_shadowRig, transform, _settings.shadow);
            _locomotion = new AgentLocomotion(_agent, clock, _settings.locomotion);
        }

        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public bool IsMoving => IsConstructed && _locomotion.IsMoving;
        public LocomotionMode Mode => IsConstructed ? _locomotion.Mode : LocomotionMode.None;

        public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds) =>
            Constructed()._locomotion.TryStartMoveTo(worldTarget, stopDistance, out etaSeconds);

        public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) => Constructed()._locomotion.WhenArrived(ct);

        public void Follow(float distanceMin, float distanceMax) => Constructed()._locomotion.Follow(distanceMin, distanceMax);

        public void Stop() => Constructed()._locomotion.Stop();

        public void LookAt(LookTarget target) => _lookTarget = target;

        public UniTask PlayGesture(GestureId gesture, CancellationToken ct) =>
            gesture == GestureId.LookAround ? LookAround(ct) : PlayClip(gesture, ct);

        public void SetMood(Mood mood)
        {
            _mood = mood;
            Constructed()._parameters.SetMood(mood);
        }

        public void SetConversationState(ConversationState state)
        {
            _conversation = state;
            Constructed()._parameters.SetConversationState(state);
        }

        void Update()
        {
            if (!IsConstructed)
                return;
            _locomotion.Tick(_user.Head.position);

            var velocity = Vector3.ProjectOnPlane(_locomotion.Velocity, Vector3.up);
            _speed = velocity.magnitude;
            if (_speed > 0.05f)
                TurnToward(Quaternion.LookRotation(velocity, Vector3.up), Time.deltaTime);
            _parameters.SetLocomotion(_speed, Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!IsConstructed)
                return;
            var dt = Time.deltaTime;
            var thinking = _conversation == ConversationState.Thinking
                           || (_gestures.IsPlaying && _gestures.Current == GestureId.Think);
            var bodyYaw = _look.Apply(TryResolveLookPoint(out var point), point, thinking, dt);
            if (bodyYaw != 0f && !_locomotion.IsMoving)
                TurnToward(Quaternion.AngleAxis(bodyYaw, Vector3.up) * transform.rotation, dt);

            _mouth.Step(_lipSync.Current, dt);
            _mixer.Mix(_expression.Select(_speed), MoodFace.For(_mood), _mouth, _blink.Tick(dt), dt);
            _faceRenderer.Write(_mixer.Output);
            _shadow.Update();
        }

        void OnDestroy()
        {
            if (IsConstructed)
                _locomotion.Stop();
        }

        async UniTask PlayClip(GestureId gesture, CancellationToken ct)
        {
            Constructed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            await _gestures.Play(gesture, linked.Token);
        }

        /// <summary>A newer sweep supersedes a running one, whose awaiter then completes normally, like a clip gesture.</summary>
        async UniTask LookAround(CancellationToken ct)
        {
            Constructed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            var generation = ++_lookAroundGeneration;
            var forward = transform.forward;
            var origin = _bones.head.position;
            try
            {
                foreach (var yaw in LookAroundYaws)
                {
                    _lookOverride = origin + Quaternion.AngleAxis(yaw, Vector3.up) * forward * 2f;
                    _hasLookOverride = true;
                    await _clock.Delay(LookAroundHold, linked.Token);
                    if (generation != _lookAroundGeneration)
                        return;
                }
            }
            finally
            {
                if (generation == _lookAroundGeneration)
                    _hasLookOverride = false;
            }
        }

        bool TryResolveLookPoint(out Vector3 point)
        {
            point = default;
            if (_hasLookOverride)
            {
                point = _lookOverride;
                return true;
            }
            switch (_lookTarget.Kind)
            {
                case LookTargetKind.WorldPoint:
                    point = _lookTarget.Point;
                    return true;
                case LookTargetKind.User:
                    point = _user.Head.position;
                    return true;
                default:
                    point = _user.Head.position;
                    return _locomotion.Mode == LocomotionMode.Following;
            }
        }

        void TurnToward(Quaternion target, float deltaTime)
        {
            var flat = Vector3.ProjectOnPlane(target * Vector3.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat, Vector3.up),
                                                          _settings.locomotion.turnSpeed * deltaTime);
        }

        NubiCharacterController Constructed() =>
            IsConstructed ? this : throw new InvalidOperationException("NubiCharacterController.Construct was not called.");
    }
}
