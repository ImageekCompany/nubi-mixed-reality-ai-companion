using System;
using System.Collections.Generic;
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
    /// frame. Update: relocation pop → locomotion → body yaw (a spin's, else the walk's) → Animator parameters. The Animator then evaluates, and
    /// LateUpdate layers the look-at on the animated bones (attending the user unless a tool's point holds, ADR-0041),
    /// writes the face (clip preset/mood → lip-sync → blink), shapes the contact shadow from the animated feet and picks
    /// the fur's shell count for the user's distance (ADR-0045).
    /// Everything character-specific comes from its <see cref="CharacterDefinition"/>: the prefab ships Nubi's model and
    /// definition, and <see cref="TryUse"/> swaps in another before <see cref="Construct"/> (ADR-0026).
    /// Inert until <see cref="Construct"/> is called by the composition root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NubiCharacterController : MonoBehaviour, ICharacterController
    {
        static readonly float[] LookAroundYaws = { -40f, 40f, 0f };
        static readonly TimeSpan LookAroundHold = TimeSpan.FromSeconds(0.8);
        /// <summary>Never zero: a zero scale makes the transform matrix singular.</summary>
        const float PopMinScale = 0.01f;

        const string ModelName = "Model";

        [SerializeField] CharacterDefinition _definition;
        [Tooltip("The instance of the definition's model, a child of this root.")]
        [SerializeField] GameObject _model;
        [SerializeField] NavMeshAgent _agent;
        [SerializeField] CapsuleCollider _collider;
        [SerializeField] MeshRenderer _contactShadow;

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
        ContactShadow _shadow;
        List<ShellFur> _fur;
        LookTarget _lookTarget = LookTarget.User;
        double _lookTargetSince;
        Mood _mood;
        ConversationState _conversation;
        bool _hasLookOverride;
        Vector3 _lookOverride;
        int _lookAroundGeneration;
        SpinMotion _spin;
        float _speed;
        Vector3 _scale;
        Vector3 _popTarget;
        float _popElapsed = -1f;
        bool _popMoved;

        public bool IsConstructed => _locomotion != null;
        public CharacterDefinition Definition => _definition;
        public NubiCharacterSettings Settings => _definition != null ? _definition.tuning : null;
        /// <summary>Null until the character is bound by <see cref="TryUse"/> or <see cref="Construct"/>.</summary>
        public Animator Animator => _rig?.Animator;
        public SkinnedMeshRenderer FaceMesh => _rig?.Face;
        /// <summary>Null until <see cref="Construct"/>, and when the character has no feet bones to follow.</summary>
        public ContactShadowRig ShadowRig { get; private set; }
        /// <summary>What the look resolved to on the last frame.</summary>
        public AttentionTarget Attending { get; private set; }

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

            CharacterBody.Apply(_definition, _model.transform, _agent, _collider,
                _contactShadow != null ? _contactShadow.transform : null);
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
            if (_contactShadow != null && _rig.HasFeet)
            {
                // After CharacterBody.Apply, so the shadow's standing size is this character's.
                ShadowRig = new ContactShadowRig
                {
                    shadow = _contactShadow, footLeft = _rig.FootLeft, footRight = _rig.FootRight,
                    footRestHeight = _rig.FootRestHeight * _definition.scale,
                };
                _shadow = new ContactShadow(ShadowRig, transform, _settings.shadow);
            }
            _fur = ShellFur.AttachAll(_model);
            _locomotion = new AgentLocomotion(_agent, clock, _settings.locomotion);
            _scale = transform.localScale;
        }

        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public bool IsMoving => IsConstructed && _locomotion.IsMoving;
        public LocomotionMode Mode => IsConstructed ? _locomotion.Mode : LocomotionMode.None;

        public bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds) =>
            Walking(Constructed()._locomotion.TryStartMoveTo(worldTarget, stopDistance, out etaSeconds));

        public bool TryStartMoveVia(Vector3 waypoint, Vector3 worldTarget, float stopDistance, float minUserDistance,
                                    out float etaSeconds) =>
            Walking(Constructed()._locomotion.TryStartMoveVia(waypoint, worldTarget, stopDistance, minUserDistance,
                                                               out etaSeconds));

        public UniTask<MoveOutcome> WhenArrived(CancellationToken ct) => Constructed()._locomotion.WhenArrived(ct);

        public void Follow(float distanceMin, float distanceMax)
        {
            Constructed()._locomotion.Follow(distanceMin, distanceMax);
            Walking(true);
        }

        public void Stop() => Constructed()._locomotion.Stop();

        public void Relocate(Vector3 floorPoint)
        {
            Constructed();
            _popTarget = floorPoint;
            if (_popElapsed >= 0f && !_popMoved)
                return;
            _popElapsed = 0f;
            _popMoved = false;
        }

        /// <summary>A point holds for <see cref="LookSettings.pointHoldSeconds"/> of standing still, then the user again.</summary>
        public void LookAt(LookTarget target)
        {
            _lookTarget = target;
            _lookTargetSince = _clock?.Now ?? 0;
        }

        public UniTask PlayGesture(GestureId gesture, CancellationToken ct) =>
            gesture == GestureId.LookAround ? LookAround(ct) : PlayClip(gesture, 0f, ct);

        public UniTask PlayLooped(GestureId gesture, float seconds, CancellationToken ct) =>
            gesture == GestureId.LookAround ? LookAround(ct) : PlayClip(gesture, seconds, ct);

        /// <summary>LookAround is a look-at sweep every character can do.</summary>
        public bool CanPlay(GestureId gesture) => gesture == GestureId.LookAround || Constructed()._gestures.CanPlay(gesture);

        /// <summary>The head turns with the body while spinning; afterwards attention faces the user again.</summary>
        public async UniTask Spin(float degrees, CancellationToken ct)
        {
            Constructed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            var spin = _spin = new SpinMotion(degrees);
            try
            {
                while (_spin == spin && !spin.Done)
                    await UniTask.Yield(PlayerLoopTiming.Update, linked.Token);
            }
            finally
            {
                if (_spin == spin)
                    _spin = null;
            }
        }

        public void SetMood(Mood mood)
        {
            _mood = mood;
            Constructed()._parameters.SetMood(mood);
        }

        /// <summary>Listening drops a held point and a look-around sweep: the user is talking.</summary>
        public void SetConversationState(ConversationState state)
        {
            _conversation = state;
            Constructed()._parameters.SetConversationState(state);
            if (state != ConversationState.Listening)
                return;
            if (_lookTarget.Kind == LookTargetKind.WorldPoint)
                _lookTarget = LookTarget.User;
            _lookAroundGeneration++;
            _hasLookOverride = false;
        }

        /// <summary>
        /// Unsigned yaw, degrees, from the body's facing and from the head's to the user's head; false before
        /// <see cref="Construct"/>. The head's facing is its bind-pose forward carried by the animated, looked bone.
        /// </summary>
        public bool TryMeasureUser(out float bodyYawOff, out float headYawOff)
        {
            bodyYawOff = headYawOff = float.NaN;
            if (!IsConstructed)
                return false;
            var head = _bones.head;
            var toUser = _user.Head.position - head.position;
            bodyYawOff = Mathf.Abs(LookAtSolver.Yaw(transform.forward, toUser, Vector3.up));
            headYawOff = Mathf.Abs(LookAtSolver.Yaw(head.rotation * _rig.HeadForward, toUser, Vector3.up));
            return true;
        }

        void Update()
        {
            if (!IsConstructed)
                return;
            TickPop(Time.deltaTime);
            _locomotion.Tick(_user.Head.position);

            var velocity = Vector3.ProjectOnPlane(_locomotion.Velocity, Vector3.up);
            _speed = velocity.magnitude;
            if (_spin != null)
                transform.rotation = Quaternion.AngleAxis(_spin.Step(Time.deltaTime), Vector3.up) * transform.rotation;
            else if (_speed > 0.05f)
                TurnToward(Quaternion.LookRotation(velocity, Vector3.up), Time.deltaTime);
            _parameters.SetLocomotion(_speed, Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!IsConstructed)
                return;
            var dt = Time.deltaTime;
            var point = default(Vector3);
            var thinking = _conversation == ConversationState.Thinking
                           || _gestures.IsPlaying(GestureId.Think);
            Attending = _spin != null ? AttentionTarget.None : ResolveAttention(out point);
            var bodyYaw = _look.Apply(Attending != AttentionTarget.None, point, thinking, dt,
                                      face: Attending == AttentionTarget.User);
            if (bodyYaw != 0f && !_locomotion.IsMoving)
                transform.rotation = Quaternion.AngleAxis(Attention.BodyTurnStep(bodyYaw, _settings.look.bodyTurnSpeed, dt),
                                                          Vector3.up) * transform.rotation;

            _mouth.Step(_lipSync.Current, dt);
            _mixer.Mix(_expression.Select(_speed), MoodFace.For(_mood), _mouth, _blink.Tick(dt), dt);
            _faceRenderer.Write(_mixer.Output);
            _shadow?.Update();
            var viewer = _user.Head.position;
            foreach (var fur in _fur)
                fur.Tick(viewer, dt);
        }

        void OnDestroy()
        {
            if (!IsConstructed)
                return;
            _locomotion.Stop();
            foreach (var fur in _fur)
                fur.Dispose();
        }

        async UniTask PlayClip(GestureId gesture, float holdSeconds, CancellationToken ct)
        {
            Constructed();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, destroyCancellationToken);
            await _gestures.Play(gesture, linked.Token, holdSeconds);
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

        /// <summary>A look-around sweep first; a point's hold restarts while walking, so it counts from the arrival.</summary>
        AttentionTarget ResolveAttention(out Vector3 point)
        {
            if (_hasLookOverride)
            {
                point = _lookOverride;
                return AttentionTarget.Point;
            }
            if (_locomotion.IsMoving)
                _lookTargetSince = _clock.Now;
            var user = _user.Head.position;
            var held = _clock.Now - _lookTargetSince < _settings.look.pointHoldSeconds;
            var attention = Attention.Resolve(_lookTarget.Kind, held, _conversation != ConversationState.Idle,
                                              _locomotion.Mode == LocomotionMode.Following,
                                              Vector3.ProjectOnPlane(user - transform.position, Vector3.up).magnitude,
                                              _settings.look);
            point = attention == AttentionTarget.Point ? _lookTarget.Point : user;
            return attention;
        }

        /// <summary>Shrinks to nothing, moves at the midpoint, grows back.</summary>
        void TickPop(float deltaTime)
        {
            if (_popElapsed < 0f)
                return;
            _popElapsed += deltaTime;
            var half = Mathf.Max(_settings.locomotion.relocatePopSeconds * 0.5f, 1e-3f);
            if (!_popMoved && _popElapsed >= half)
            {
                _locomotion.Relocate(_popTarget);
                _popMoved = true;
            }
            if (_popElapsed >= 2f * half)
            {
                _popElapsed = -1f;
                transform.localScale = _scale;
                return;
            }
            var size = Mathf.SmoothStep(0f, 1f, Mathf.Abs(_popElapsed - half) / half);
            transform.localScale = _scale * Mathf.Max(size, PopMinScale);
        }

        void TurnToward(Quaternion target, float deltaTime)
        {
            var flat = Vector3.ProjectOnPlane(target * Vector3.forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f)
                return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat, Vector3.up),
                                                          _settings.locomotion.turnSpeed * deltaTime);
        }

        /// <summary>A walk that starts ends a whole-body gesture or spin in progress (ADR-0047): played while the agent
        /// moves, it slides the character across the floor mid-jump.</summary>
        bool Walking(bool started)
        {
            if (started)
            {
                _gestures.EndAction();
                _spin = null;
            }
            return started;
        }

        NubiCharacterController Constructed() =>
            IsConstructed ? this : throw new InvalidOperationException("NubiCharacterController.Construct was not called.");
    }
}
