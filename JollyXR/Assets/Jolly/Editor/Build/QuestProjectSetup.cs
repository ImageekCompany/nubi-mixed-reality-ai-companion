using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.Meta;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Jolly.Editor.Build
{
    /// <summary>
    /// Idempotent Quest 3 project configuration. Safe to re-run; every step checks before it writes.
    /// Batchmode: <c>Unity -batchmode -quit -projectPath JollyXR -buildTarget Android -executeMethod Jolly.Editor.Build.QuestProjectSetup.Apply</c>
    /// </summary>
    public static class QuestProjectSetup
    {
        public const string PackageName = "com.nubi.companion";
        public const string CompanyName = "Nubi Open Source";
        public const string ProductName = "Nubi";

        const string RenderingFolder = "Assets/Jolly/Settings/Rendering";
        const string UrpAssetPath = RenderingFolder + "/NubiURP_Quest.asset";
        const string UrpRendererPath = RenderingFolder + "/NubiURP_Quest_Renderer.asset";
        const string XRSettingsFolder = "Assets/XR";
        const string XRGeneralSettingsPath = XRSettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset";
        const string Quest3ManifestName = "eureka";
        const string Quest3SManifestName = "quest3s";
        const string LogTag = "[QuestProjectSetup]";

        // Feature ids verified against Library/PackageCache sources (openxr 1.18.0, meta-openxr 2.6.1, hands 1.9.0).
        static readonly string[] EnabledFeatureIds =
        {
            MetaQuestFeature.featureId,
            ARSessionFeature.featureId,
            ARCameraFeature.featureId,
            // OpenXRCompositionLayersFeature.FeatureId (the type only compiles under XR_COMPOSITION_LAYERS, hence the
            // literal). meta-openxr draws passthrough as a composition layer; with this off the camera starts but the
            // compositor gets no passthrough layer and the user sees black.
            "com.unity.openxr.feature.compositionlayers",
            ARPlaneFeature.featureId,
            ARBoundingBoxFeature.featureId,
            ARRaycastFeature.featureId,
            ARAnchorFeature.featureId,
            // Environment depth for occlusion (ADR-0024). Hand removal stays off: the hands in the depth map are what
            // hides Nubi behind the user's hands.
            AROcclusionFeature.featureId,
            DisplayUtilitiesFeature.featureId,
            HandTracking.featureId2,
            MetaHandTrackingAim.featureId,
            HandInteractionProfile.featureId,
            OculusTouchControllerProfile.featureId,
            FoveatedRenderingFeature.featureId,
        };

        // MicrosoftHandInteraction.featureId (the type is [Obsolete], hence the literal). The obsolete
        // HandTracking.featureId has the same value, so enabling features by that id turned this HoloLens profile on.
        static readonly string[] DisabledFeatureIds =
        {
            "com.unity.openxr.feature.input.handtracking",
        };

        [MenuItem("Jolly/Build/Apply Quest Project Setup")]
        public static void Apply()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log($"{LogTag} Switching active build target to Android");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            ApplyPlayerSettings();
            ApplyRendering();
            ApplyXRManagement();
            ApplyOpenXRFeatures();
            AssetDatabase.SaveAssets();
            LogValidation(applyAutomaticFixes: true);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} Done");
        }

        static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            // Minimal stripping: Newtonsoft and ClientWebSocket rely on reflection-heavy code paths.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // 32 = Android 12L: minimum for meta-openxr CPU camera images (ARCameraFeature.k_CameraMinSdkVersion).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            // 34 = the device OS level (Quest 3, Horizon OS v209 reports ro.build.version.sdk=34) and the lowest platform
            // in the editor-bundled SDK (android-34/36/37); 32 would need an SDK download. Store target-API policy: UNVERIFIED.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            SetInputSystemOnly();
        }

        // No public API for "Active Input Handling"; the serialized PlayerSettings singleton is what the Input System
        // package itself edits. 1 = Input System Package only. Takes effect for scripts after an editor restart, so
        // tools/device/build.sh runs Apply and BuildApk in separate editor sessions.
        static void SetInputSystemOnly()
        {
            const int inputSystemOnly = 1;
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var so = playerSettings != null ? new SerializedObject(playerSettings) : null;
            var prop = so?.FindProperty("activeInputHandler");
            if (prop == null)
            {
                Debug.LogError($"{LogTag} Cannot find PlayerSettings.activeInputHandler");
                return;
            }
            if (prop.intValue == inputSystemOnly)
                return;
            prop.intValue = inputSystemOnly;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"{LogTag} Active Input Handling set to Input System Package (restart required)");
        }

        static void ApplyRendering()
        {
            EnsureFolder(RenderingFolder);

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
            }
            rendererData.postProcessData = null;
            rendererData.intermediateTextureMode = IntermediateTextureMode.Auto;
            EditorUtility.SetDirty(rendererData);

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
            }
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            urp.renderScale = 1f;
            urp.shadowDistance = 8f;
            urp.useSRPBatcher = true;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ApplyXRManagement()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (guids.Length > 0)
                    perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (perTarget == null)
            {
                EnsureFolder(XRSettingsFolder);
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, XRGeneralSettingsPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);

            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            var manager = general.Manager;
            if (!manager.activeLoaders.Any(l => l is OpenXRLoader))
            {
                var ok = XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
                Debug.Log($"{LogTag} Assign OpenXRLoader (Android): {ok}");
            }
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perTarget);
        }

        static void ApplyOpenXRFeatures()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
                throw new InvalidOperationException($"{LogTag} OpenXR settings for Android are missing after RefreshFeatures");

            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            settings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;

            foreach (var id in DisabledFeatureIds)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature != null && feature.enabled)
                {
                    feature.enabled = false;
                    EditorUtility.SetDirty(feature);
                }
            }

            foreach (var id in EnabledFeatureIds)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android, id);
                if (feature == null)
                {
                    Debug.LogError($"{LogTag} OpenXR feature not found: {id}");
                    continue;
                }
                if (!feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
            }

            var camera = settings.GetFeature<ARCameraFeature>();
            if (camera != null && !camera.cameraImageSupportEnabled)
            {
                camera.cameraImageSupportEnabled = true;
                EditorUtility.SetDirty(camera);
            }

            // Eye-tracked foveation + the Quest Pro target makes MetaQuest's manifest step add a *required*
            // oculus.software.eye_tracking feature, which Quest 3 lacks. Fixed foveation only.
            var foveation = settings.GetFeature<FoveatedRenderingFeature>();
            if (foveation != null && foveation.UseEyeTracking)
            {
                var fso = new SerializedObject(foveation);
                fso.FindProperty("useEyeTracking").boolValue = false;
                fso.ApplyModifiedPropertiesWithoutUndo();
            }

            var quest = settings.GetFeature<MetaQuestFeature>();
            if (quest != null)
                EnableQuestTargetDevices(quest);

            EditorUtility.SetDirty(settings);
        }

        // MetaQuestFeature.targetDevices / EnableTargetDevice are internal; the serialized list is the stable surface.
        static void EnableQuestTargetDevices(MetaQuestFeature quest)
        {
            var so = new SerializedObject(quest);
            var list = so.FindProperty("targetDevices");
            if (list == null || !list.isArray)
            {
                Debug.LogWarning($"{LogTag} MetaQuestFeature.targetDevices not found; leaving target devices at defaults");
                return;
            }
            for (var i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var manifestName = element.FindPropertyRelative("manifestName").stringValue;
                if (manifestName == Quest3ManifestName || manifestName == Quest3SManifestName)
                    element.FindPropertyRelative("enabled").boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static bool LogValidation(bool applyAutomaticFixes)
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            if (applyAutomaticFixes)
            {
                foreach (var issue in issues.Where(i => i.error && i.fixItAutomatic && i.fixIt != null))
                {
                    Debug.Log($"{LogTag} Auto-fix: {issue.message}");
                    issue.fixIt();
                }
                OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            }

            var errors = 0;
            foreach (var issue in issues)
            {
                if (issue.error)
                    errors++;
                Debug.Log($"{LogTag} Validation {(issue.error ? "ERROR" : "warning")}: {issue.message}");
            }
            Debug.Log($"{LogTag} OpenXR validation: {errors} error(s), {issues.Count - errors} warning(s)");
            return errors == 0;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
