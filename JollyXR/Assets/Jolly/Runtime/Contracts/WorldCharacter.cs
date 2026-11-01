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

    /// <summary>Where Nubi can walk: nowhere yet, the fallback disc around the user (no Space Setup, ADR-0025), or the
    /// scanned floor.</summary>
    public enum WalkableArea { None, Fallback, Scanned }

    /// <summary>
    /// A labelled Space Setup surface in world space: a bounding box, or a horizontal plane such as a table top or a couch
    /// seat. Pose.position is the centre of the box or plane; its yaw orients Size.x and Size.z.
    /// </summary>
    public readonly struct SceneSurface
    {
        public SceneSurface(string label, Pose pose, Vector3 size) { Label = label; Pose = pose; Size = size; }
        /// <summary>Lower case: couch, table, bed, chair, storage, screen, lamp or plant.</summary>
        public string Label { get; }
        public Pose Pose { get; }
        /// <summary>Full extents in the surface's own axes; y is 0 for a plane.</summary>
        public Vector3 Size { get; }
        public float Top => Pose.position.y + Size.y * 0.5f;
    }

    public interface IWorldQuery
    {
        WalkableArea WalkableArea { get; }
        /// <summary>ARRaycastManager.Raycast(ray, hits, TrackableType.Depth) (meta-openxr supports only Depth), then
        /// Physics.Raycast against plane/box colliders on the NavGeometry layer; nearest hit wins. False before USE_SCENE is granted.</summary>
        bool TryRaycast(Ray ray, float maxDistance, out Vector3 hit);
        /// <summary>NavMesh.SamplePosition, keeping only hits within 0.1 m of the floor plane height (never a furniture top).</summary>
        bool TrySampleWalkable(Vector3 near, float radius, out Vector3 point);   // NavMesh.SamplePosition
        /// <summary>Clears `into` and adds every labelled plane and bounding box. Empty before USE_SCENE or Space Setup.</summary>
        void GetSurfaces(List<SceneSurface> into);
    }

    public enum RoomScanStatus { Pending, Scanned, Unscanned, NoPermission }

    /// <summary>The room-scan flow (ADR-0025): whether Space Setup data exists, and a way to open Space Setup.</summary>
    public interface IRoomScan
    {
        RoomScanStatus Status { get; }
        /// <summary>False where there is no Space Setup to open (Editor, Link, another runtime).</summary>
        bool CanRequestScan { get; }
        /// <summary>Queues Space Setup. It opens once Nubi is idle and silent, so a spoken reply finishes first; the app
        /// pauses while the user scans and the scene is read again when it resumes. False if CanRequestScan is false.</summary>
        bool RequestScan();
    }

    /// <summary>The XR side of the room-scan flow, driven by Core's RoomScanMonitor.</summary>
    public interface ISceneData
    {
        bool PermissionGranted { get; }
        bool HasScannedFloor { get; }
        bool CanRequestCapture { get; }
        /// <summary>MetaOpenXRSessionSubsystem.TryRequestSceneCapture: true if the runtime accepted the request.</summary>
        bool TryRequestCapture();
        /// <summary>Restarts the plane and bounding-box queries, so a Space Setup finished outside the app is read.</summary>
        void Requery();
        /// <summary>Makes the outline (floor-plane x/z offsets around center) at the tracking-origin floor height the
        /// walkable area until <see cref="ClearFallbackFloor"/>. False if there is no floor height to use.</summary>
        bool SetFallbackFloor(Vector3 center, IReadOnlyList<Vector2> outline);
        void ClearFallbackFloor();
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
        /// <summary>Moves Nubi to floorPoint without walking, hidden by a short shrink-and-grow pop: for when it is left
        /// where no NavMesh will reach (the fallback floor, ADR-0025). A running MoveTo completes Cancelled; Follow
        /// carries on from the new spot. A call before a pop has moved Nubi only retargets it.</summary>
        void Relocate(Vector3 floorPoint);
        /// <summary>User: watch and face the user. At(point): look there for a short hold once standing still, then attend
        /// the user again. None: no target of its own; the user is still attended during a conversation turn, while
        /// following, or when near (ADR-0041). A new Listening state drops a held point.</summary>
        void LookAt(LookTarget target);
        UniTask PlayGesture(GestureId gesture, CancellationToken ct);
        void SetMood(Mood mood);
        void SetConversationState(ConversationState state);
    }
}
