using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace Jolly.Spikes.Editor
{
    /// <summary>
    /// Generates Assets/Spikes/DeviceSpike.unity and makes it the only build scene.
    /// <c>Unity -batchmode -quit -projectPath JollyXR -buildTarget Android -executeMethod Jolly.Spikes.Editor.DeviceSpikeSceneBuilder.Build</c>
    /// </summary>
    public static class DeviceSpikeSceneBuilder
    {
        const string ScenePath = "Assets/Spikes/DeviceSpike.unity";

        [MenuItem("Jolly/Spikes/Build Device Spike Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("AR Session", typeof(ARSession));

            var originGo = new GameObject("XR Origin");
            var origin = originGo.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(offset.transform, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // alpha 0 = passthrough shows through
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            cameraGo.AddComponent<AudioListener>();

            var tpd = cameraGo.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            tpd.trackingStateInput = new InputActionProperty(new InputAction("Tracking State", binding: "<XRHMD>/trackingState", expectedControlType: "Integer"));

            var cameraManager = cameraGo.AddComponent<ARCameraManager>();
            cameraManager.enabled = false; // enabled by DeviceSpike after HEADSET_CAMERA is granted

            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

            // Visible marker so a screencap shows the app is rendering over passthrough.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            marker.transform.position = new Vector3(0f, 1.2f, 1.5f);
            marker.transform.localScale = Vector3.one * 0.15f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var spikeGo = new GameObject("Device Spike");
            var audio = spikeGo.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var spike = spikeGo.AddComponent<DeviceSpike>();
            var so = new SerializedObject(spike);
            so.FindProperty("cameraManager").objectReferenceValue = cameraManager;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DeviceSpikeSceneBuilder] Saved {ScenePath} and set it as the only build scene");
        }
    }
}
