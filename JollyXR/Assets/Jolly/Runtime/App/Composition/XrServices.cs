using System;
using Jolly.Contracts;
using Jolly.XR.Hands;
using Jolly.XR.Permissions;
using Jolly.XR.World;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID && !UNITY_EDITOR
using Jolly.XR.MetaQuest;
#else
using Jolly.XR.Capture;
#endif

namespace Jolly.App.Composition
{
    /// <summary>
    /// The XR layer (architecture §2, §5.5): hands, user pose, permissions, world queries, anchors and the camera. The
    /// camera source is the passthrough CPU image on device and a still image in the Editor and over Link, where
    /// meta-openxr gives no CPU image (xr-stack.md §2.2).
    /// </summary>
    public sealed class XrServices
    {
        public IHandSource Hands { get; private set; }
        public IUserPoseProvider User { get; private set; }
        public IRuntimePermissions Permissions { get; private set; }
        public IWorldQuery World { get; private set; }
        public ISpatialAnchors Anchors { get; private set; }
        public ICameraFrameSource Camera { get; private set; }

        public static XrServices Build(Foundation f, JollyConfig config, SceneRefs scene,
                                       Func<ICameraFrameSource, ICameraFrameSource> wrapCamera, DisposableStack disposables)
        {
            var hands = new XrHandSource(scene.origin.transform);
            scene.handMonitor.Construct(f.Log, f.Clock, scene.origin.transform);
            scene.userPose.Construct(scene.head, hands, scene.controllerAim, config.headPoseLeadSeconds);
            var permissions = new AndroidRuntimePermissions();
            scene.permissions.Construct(permissions, scene.cameraManager,
                new Behaviour[] { scene.planeManager, scene.boxManager, scene.raycastManager });
            scene.floor.Construct(f.Clock);
            return new XrServices
            {
                Hands = hands,
                User = scene.userPose,
                Permissions = permissions,
                World = new ArWorldQuery(scene.raycastManager, scene.planeManager, scene.boxManager, scene.floor),
                Anchors = new ArSpatialAnchors(scene.anchorManager, f.Log),
                Camera = disposables.Add(wrapCamera(CreateCamera(f, config, scene.cameraManager, scene.userPose, permissions))),
            };
        }

        static ICameraFrameSource CreateCamera(Foundation f, JollyConfig config, ARCameraManager cameraManager,
                                               Jolly.XR.Tracking.XrUserPoseProvider user, IRuntimePermissions permissions)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new MetaPassthroughFrameSource(cameraManager, user, user, permissions, f.Clock, f.Log);
#else
            var still = config.editorCameraStill != null ? config.editorCameraStill.bytes : Array.Empty<byte>();
            return new EditorStillFrameSource(still, user, f.Clock);
#endif
        }
    }
}
