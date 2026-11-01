using System;
using Jolly.Audio.Output;
using Jolly.Character;
using Jolly.Presentation;
using Jolly.XR.Hands;
using Jolly.XR.Interaction;
using Jolly.XR.Navigation;
using Jolly.XR.Occlusion;
using Jolly.XR.Permissions;
using Jolly.XR.Tracking;
using Jolly.XR.Voice;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Jolly.App
{
    /// <summary>The scene objects <see cref="JollyBootstrap"/> wires. Main.unity binds every field; validators check it.</summary>
    [Serializable]
    public sealed class SceneRefs
    {
        [Header("XR rig")]
        public XROrigin origin;
        public Transform head;
        [Tooltip("Controller ray origin; null for hands only.")]
        public Transform controllerAim;
        public XrUserPoseProvider userPose;
        public PinchPushToTalk pushToTalk;
        public ControllerPushToTalk controllerPushToTalk;
        public PermissionGate permissions;
        public FloorNavMeshBuilder floor;
        public RealWorldOcclusion occlusion;
        public InteractionSignals interactionSignals;
        public HandTrackingMonitor handMonitor;

        [Header("AR managers (ship disabled; PermissionGate enables them)")]
        public ARCameraManager cameraManager;
        public ARPlaneManager planeManager;
        public ARBoundingBoxManager boxManager;
        public ARRaycastManager raycastManager;
        public ARAnchorManager anchorManager;

        [Header("Nubi")]
        public NubiCharacterController nubi;
        public StreamingSpeechOutput speechOutput;
        [Tooltip("uLipSync after StreamingSpeechOutput on the same GameObject; null uses the amplitude driver.")]
        public uLipSync.uLipSync lipSync;
        public Collider nubiCollider;
        public XRBaseInteractable nubiInteractable;

        [Header("Views")]
        public CaptionView captions;
        public ToolBadgeView toolBadges;
        public LatencyHud latencyHud;
        public MaskOverlayView maskOverlay;
        public StatusIndicator status;
        public SettingsPanel settings;
    }
}
