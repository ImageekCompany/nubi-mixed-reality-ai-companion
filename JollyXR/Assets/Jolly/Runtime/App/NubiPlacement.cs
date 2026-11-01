using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Jolly.Contracts;
using UnityEngine;

namespace Jolly.App
{
    /// <summary>
    /// Puts Nubi on the floor in front of the user, facing them, once the head is tracked. The rig uses the Floor
    /// tracking origin, so the floor is the XR origin's height. The NavMesh agent attaches itself once the scanned floor
    /// is baked (WP6), so no NavMesh is needed here.
    /// </summary>
    public static class NubiPlacement
    {
        public const float Distance = 1.2f;
        // A seated user's eyes are still well above this; an untracked head reports the origin.
        const float TrackedHeadMinHeight = 0.5f;
        static readonly TimeSpan TrackingWait = TimeSpan.FromSeconds(5);

        public static async UniTaskVoid PlaceInFrontOfUser(SceneRefs scene, IClock clock, CancellationToken ct)
        {
            var deadline = clock.Now + TrackingWait.TotalSeconds;
            var floorY = scene.origin.transform.position.y;
            while (scene.head.position.y - floorY < TrackedHeadMinHeight && clock.Now < deadline)
            {
                if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                    return;
            }
            var pose = InFrontOf(new Pose(scene.head.position, scene.head.rotation), floorY, Distance);
            scene.nubi.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>On the floor <paramref name="distance"/> ahead of the head's yaw, turned to face back at the head.</summary>
        public static Pose InFrontOf(Pose head, float floorY, float distance)
        {
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();
            var position = new Vector3(head.position.x, floorY, head.position.z) + forward * distance;
            return new Pose(position, Quaternion.LookRotation(-forward, Vector3.up));
        }
    }
}
