using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Jolly.Character
{
    /// <summary>
    /// The companion's <see cref="ICharacterController"/>: a thin MonoBehaviour shell that orders the plain-C# parts per
    /// frame. Update: locomotion → body yaw → Animator parameters. The Animator then evaluates, and LateUpdate layers the
    /// look-at on the animated bones and writes the face (clip preset/mood → lip-sync → blink).
    /// Everything character-specific comes from its <see cref="CharacterDefinition"/>: the prefab ships Nubi's model and
    /// definition, and <see cref="TryUse"/> swaps in another before <see cref="Construct"/> (ADR-0026).
    /// Inert until <see cref="Construct"/> is called by the composition root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NubiCharacterController : MonoBehaviour, ICharacterController
    {
        static readonly float[] LookAroundYaws = { -40f, 40f, 0f };
        static readonly TimeSpan LookAroundHold = TimeSpan.FromSeconds(0.8);

        const string ModelName = "Model";

        [SerializeField] CharacterDefinition _definition;
        [Tooltip("The instance of the definition's model, a child of this root.")]
        [SerializeField] GameObject _model;
        [SerializeField] NavMeshAgent _agent;
        [SerializeField] CapsuleCollider _collider;
        [SerializeField] Transform _contactShadow;

        IUserPoseProvider _user;
        ILipSyncDriver _lipSync;
        IClock _clock;
        CharacterRig _rig;
        NubiRigBones _bones;
        NubiCharacterSettings _settings;
        AgentLocomotion _locomotion;
        CharacterAnimator _parameters;
        GestureDriver _gestures;
        LookAtDriver _look;
        ClipExpressionSelector _expression;
        MouthSmoother _mouth;
        BlinkScheduler _blink;
        FaceMixer _mixer;
        FaceRenderer _faceRenderer;
        LookTarget _lookTarget = LookTarget.User;
        Mood _mood;
        ConversationState _conversation;
        bool _hasLookOverride;
        Vector3 _lookOverride;
        int _lookAroundGeneration;
        float _speed;

        public bool IsConstructed => _locomotion != null;
        public CharacterDefinition Definition => _definition;
        public NubiCharacterSettings Settings => _definition != null ? _definition.tuning : null;
        /// <summary>Null until the character is bound by <see cref="TryUse"/> or <see cref="Construct"/>.</summary>
        public Animator Animator => _rig?.Animator;
        public SkinnedMeshRenderer FaceMesh => _rig?.Face;

        /// <summary>
        /// Swaps in another character before <see cref="Construct"/>: instantiates its model under this root and binds
        /// it. On failure nothing changes and <paramref name="error"/> says why.
        /// </summary>
        public bool TryUse(CharacterDefinition character, out string error)
        {
            if (IsConstructed)
                throw new InvalidOperationException("Swap the character before Construct.");
            if (character == null)
            {
                error = "no definition";
                return false;
            }
            if (character == _definition)
            {
                error = null;
                return true;
            }
            if (!character.IsUsable(out error))
                return false;

            var model = Instantiate(character.model, transform, false);
            model.name = ModelName;
            if (!CharacterRig.TryBind(model, character, out var rig, out error))
            {
                model.SetActive(false);
                Destroy(model);
                return false;
            }
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.skinnedMotionVectors = false;
            }
            if (_model != null)
            {
                // Destroy is deferred to the end of the frame; inactive, the old Animator stops at once.
                _model.SetActive(false);
                Destroy(_model);
            }
            _model = model;
            _definition = character;
            _rig = rig;
            return true;
        }

        public void Construct(IUserPoseProvider user, ILipSyncDriver lipSync, IClock clock, IRandom random)
        {
            if (IsConstructed)
                throw new InvalidOperationException("NubiCharacterController is already constructed.");
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _lipSync = lipSync ?? throw new ArgumentNullException(nameof(lipSync));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (_definition == null || _model == null || _agent == null)
                throw new InvalidOperationException("Character prefab references are not bound; rebuild it with Jolly/Character/Rebuild Nubi Assets.");
            if (!_definition.IsUsable(out var unusable))
                throw new InvalidOperationException($"Character '{_definition.name}' is not usable: {unusable}.");
            if (_rig == null && !CharacterRig.TryBind(_model, _definition, out _rig, out var unbound))
                throw new InvalidOperationException($"Character '{_definition.id}' does not bind: {unbound}.");

            CharacterBody.Apply(_definition, _model.transform, _agent, _collider, _contactShadow);
            _settings = _definition.tuning;
            _bones = _rig.Bones;
            var animator = _rig.Animator;
            var presets = FacePresetTable.Parse(_definition.facePresets.text);
            _parameters = new CharacterAnimator(animator, _settings.locomotion);
            _gestures = new GestureDriver(animator);
            _look = new LookAtDriver(_bones, transform, _settings.look);
            _expression = new ClipExpressionSelector(animator, presets, _settings.locomotion);
            _mouth = new MouthSmoother(_settings.face);
            _blink = new BlinkScheduler(random, _settings.face);
            _mixer = new FaceMixer(_settings.face);
            _faceRenderer = new FaceRenderer(_rig.Face, _definition.face, _rig.Jaw, _definition.jaw.openRotation);
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
