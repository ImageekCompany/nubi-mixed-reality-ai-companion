using System;
using UnityEngine;

namespace Jolly.Character
{
    [Serializable]
    public sealed class LocomotionSettings
    {
        [Tooltip("NavMeshAgent cruise speed, m/s (architecture §5.5).")]
        public float walkSpeed = 0.6f;
        [Tooltip("Follow-mode speed while the user is farther than catchUpDistance, m/s. Hop at 1.5x tops out near 1.35.")]
        public float catchUpSpeed = 1.2f;
        public float catchUpDistance = 2.5f;
        public float acceleration = 3f;
        [Tooltip("How far from a requested point a walkable NavMesh point may be, m.")]
        public float sampleRadius = 0.5f;
        [Tooltip("Body yaw rate while turning in place or walking, deg/s.")]
        public float turnSpeed = 240f;
        [Tooltip("A move reports TimedOut after 2 x ETA + this slack, s.")]
        public float timeoutSlack = 3f;
        public float followRepathInterval = 0.25f;
        public float followRepathDistance = 0.3f;
        [Tooltip("Relocate: Nubi shrinks away for half of this, moves, and grows back for the other half, s.")]
        public float relocatePopSeconds = 0.4f;

        [Header("Clip playback (docs/art/nubi/README.md, Unity notes)")]
        [Tooltip("Root speed the Walk clip matches at 1x, m/s.")]
        public float walkClipSpeed = 0.49f;
        [Tooltip("Root speed the Hop clip matches at 1x, m/s.")]
        public float hopClipSpeed = 0.90f;
        [Tooltip("Above this agent speed the gait is Hop, m/s.")]
        public float hopAbove = 0.65f;
        public float minPlaybackRate = 0.7f;
        [Tooltip("Past ~1.5x the planted feet visibly flicker.")]
        public float maxPlaybackRate = 1.5f;
    }

    [Serializable]
    public sealed class LookSettings
    {
        [Tooltip("Eye bones beyond this sink the outer eye into the head (README, Unity notes).")]
        public float eyeYawMax = 16f;
        public float eyePitchMax = 8f;
        [Tooltip("The eyes take only the last few degrees; the head chain takes the rest.")]
        public float eyeLead = 10f;
        public float headYawMax = 40f;
        public float headPitchMax = 20f;
        [Tooltip("The body turns in place when the target is farther off-axis than this, deg.")]
        public float bodyTurnBeyond = 45f;
        [Tooltip("Body turning stops once the target is within this, deg.")]
        public float bodySettleWithin = 5f;
        public float blendSeconds = 0.25f;
        [Range(0f, 1f)] public float chestShare = 0.2f;
        [Range(0f, 1f)] public float neckShare = 0.3f;
    }

    [Serializable]
    public sealed class FaceSettings
    {
        public float presetFadeSeconds = 0.15f;
        public float mouthAttackSeconds = 0.06f;
        public float mouthReleaseSeconds = 0.09f;
        public float blinkIntervalMin = 2f;
        public float blinkIntervalMax = 6f;
        [Range(0f, 1f)] public float doubleBlinkChance = 0.15f;
        public float doubleBlinkGap = 0.12f;
        public float blinkCloseSeconds = 0.06f;
        public float blinkHoldSeconds = 0.03f;
        public float blinkOpenSeconds = 0.09f;
        [Tooltip("Blinks are suppressed while Happy_Eyes is above this (the eyes are already closed arcs).")]
        public float happyEyesBlinkSuppress = 0.5f;
    }

    [Serializable]
    public sealed class NubiCharacterSettings
    {
        public LocomotionSettings locomotion = new();
        public LookSettings look = new();
        public FaceSettings face = new();
    }
}
