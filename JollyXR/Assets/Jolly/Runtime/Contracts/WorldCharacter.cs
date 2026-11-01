using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Jolly.Contracts
{
    public interface IUserPoseProvider
    {
        Pose Head { get; }
        /// <summary>Index-finger aim ray of whichever hand is pointing (or the controller ray). False if neither hand is pointing.</summary>
        bool TryGetPointingRay(out Ray ray);
        /// <summary>Clears `into` and adds world positions of a few joints (wrist, palm, finger tips) of each tracked hand,
        /// for SAM negative points. No allocation when `into` has capacity.</summary>
        void GetHandKeyPoints(List<Vector3> into);
    }

    public interface IWorldQuery
    {
        bool HasWalkableArea { get; }
        /// <summary>ARRaycastManager.Raycast(ray, hits, TrackableType.Depth) (meta-openxr supports only Depth), then
        /// Physics.Raycast against plane/box colliders on the NavGeometry layer; nearest hit wins. False before USE_SCENE is granted.</summary>
        bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit);
        /// <summary>NavMesh.SamplePosition, keeping only hits within 0.1 m of the floor plane height (never a furniture top).</summary>
        bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point);   // NavMesh.SamplePosition
        /// <summary>label: AR plane/box classification, case-insensitive ("table", "couch", "bed", "storage", "screen").</summary>
        bool TryFindLabeledSurface(string label, Vector3 near, out Vector3 point);
    }

    /// <summary>Raw interaction detections from the XR layer (WP5); InteractionTriggers (WP10) rate-limits them into canned lines.
    /// Raised on the main thread.</summary>
    public interface IInteractionSignals
    {
        event Action GazeDwell;   // gaze on Nubi >= 1.5 s
        event Action OpenPalm;    // XR Hands static gesture toward Nubi
        event Action Poke;        // XRI poke on Nubi's collider
    }

    public interface ISpatialAnchors
    {
        /// <summary>Creates and persists an anchor. Returns its id, or null if persistence is unsupported (the caller keeps the pose only).</summary>
        UniTask<string> CreateAndSave(Pose pose, CancellationToken ct);
        UniTask<Pose?> TryResolve(string anchorId, CancellationToken ct);
        /// <summary>Deletes the persisted anchor. An unknown id, or a platform without erase, is a no-op.</summary>
        UniTask Erase(string anchorId, CancellationToken ct);
    }

    public enum ConversationState { Idle, Listening, Thinking, Speaking, Acting }
    public enum Mood { Neutral, Happy, Curious, Concerned, Sleepy }
    public enum GestureId { Wave, Nod, ShakeHead, Shrug, Point, HappyBounce, Think, Stretch, LookAround }
    public enum MoveOutcome { Arrived, Unreachable, Cancelled, TimedOut }
    public enum LookTargetKind { None, User, WorldPoint }

    public readonly struct LookTarget
    {
        LookTarget(LookTargetKind kind, Vector3 point) { Kind = kind; Point = point; }
        public LookTargetKind Kind { get; }
        public Vector3 Point { get; }
        public static LookTarget None => default;
        public static LookTarget User => new(LookTargetKind.User, default);
        public static LookTarget At(Vector3 world) => new(LookTargetKind.WorldPoint, world);
    }

    public enum LocomotionMode { None, MovingTo, Following }

    public interface ICharacterController
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        bool IsMoving { get; }
        LocomotionMode Mode { get; }
        /// <summary>
        /// Supersedes any running locomotion. Returns false (Unreachable) if no path exists, otherwise starts the motion
        /// on the controller's own commandCts and returns true with the ETA. The motion ends only on arrival, Stop(), or a newer
        /// MoveTo/Follow; no caller token can stop it, so a barge-in never halts a walk.
        /// </summary>
        bool TryStartMoveTo(Vector3 worldTarget, float stopDistance, out float etaSeconds);
        /// <summary>Awaits the current motion's outcome. ct only detaches this awaiter; it never stops the motion.
        /// Returns Cancelled if superseded or stopped.</summary>
        UniTask<MoveOutcome> WhenArrived(CancellationToken ct);
        /// <summary>Follow mode until Stop() or another MoveTo/Follow. Keeps distanceMin..distanceMax from the user.</summary>
        void Follow(float distanceMin, float distanceMax);
        /// <summary>Halts locomotion immediately (StopGuard, stop tool, Settings).</summary>
        void Stop();
        void LookAt(LookTarget target);
        UniTask PlayGesture(GestureId gesture, CancellationToken ct);
        void SetMood(Mood mood);
        void SetConversationState(ConversationState state);
    }
}
