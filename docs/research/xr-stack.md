# XR stack research: Unity 6.6 + OpenXR + Unity OpenXR: Meta on Quest 3

Date: 2026-11-01. Editor: 6000.6.6f1. Device: Quest 3, Horizon OS v209.
Scope: the package set, Passthrough Camera Access (PCA) for SAM input, mapping pixels to world points, scene understanding, NavMesh, hands, microphone, project settings.

Abbreviations used below:
- `EDITOR` = `/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/Resources/PackageManager`
- `MO` = the meta-openxr 2.7.0 tarball, `https://download.packages.unity.com/com.unity.xr.meta-openxr/-/com.unity.xr.meta-openxr-2.7.0.tgz`. I unpacked it, and also the 2.6.1 tarball from `EDITOR/Editor/`, which is identical for everything cited here unless a section says otherwise.
- `UNVERIFIED` = no primary source found. Treat it as an assumption to test on the device.

---

## 0. TL;DR / decisions

1. **Use the editor-bundled XR package set** (table in §1). The editor ships these as tarballs, so they need no download, and `EDITOR/Editor/manifest.json` marks them `mustBeBundled`, meaning Unity verified them for 6000.6. If we need a 2.7.0 fix, upgrade the whole set to `openxr 1.19.0 / meta-openxr 2.7.0 / arfoundation 6.7.1` in one change. Do not mix versions.
2. **Primary PCA path:** `ARCameraManager` + `MetaOpenXRCameraSubsystem.TryAcquireLatestCpuImage` → `XRCpuImage.ConvertAsync` → JPEG → Meta API / SAM. All of it is OpenXR-based and under the Unity Companion License.
3. **Fallback PCA path:** `WebCamTexture`, a plain Unity API that Meta documents for PCA. It needs no Meta SDK, so the license stays clean. Meta's MRUK `PassthroughCameraAccess` is a second fallback only, because it is under the Oculus SDK License and needs OVRPlugin.
4. **Gap: meta-openxr exposes camera intrinsics but no extrinsics (lens pose).** We get the lens pose ourselves from Android Camera2 `CameraCharacteristics.LENS_POSE_TRANSLATION/ROTATION` via JNI, using the same pattern Meta's sample used (§2.5). We then raycast into the world with `ARRaycastManager` against `TrackableType.Depth`.
5. **Permissions gotcha:** the camera subsystem checks `horizonos.permission.HEADSET_CAMERA` only once, in `Start()`. Keep `ARCameraManager` disabled until the permission is granted, or restart it after the grant (§2.3).
6. **"Walk to the kitchen":** the OpenXR scene model has no room labels such as "kitchen". Use user-taught persistent anchors (§4.4).

---

## 1. Package versions for Unity 6000.6

Sources:
- `curl https://packages.unity.com/<name>`, parsing `dist-tags.latest` and `time`.
- `EDITOR/Editor/manifest.json` and `ls EDITOR/Editor/*.tgz`, for the editor-bundled tarballs.

| Package | Bundled with 6000.6.6f1 (recommended pin) | Latest on registry (publish date) | Notes |
|---|---|---|---|
| com.unity.xr.openxr | **1.18.0** | 1.19.0 (2026-10-26) | |
| com.unity.xr.meta-openxr | **2.6.1** | 2.7.0 (2026-10-26) | 2.7.0 depends on openxr ≥1.18.0, arfoundation ≥6.6.0, management ≥4.7.0, compositionlayers ≥2.4.0. 2.6.1 is the minimum version for Meta VR Glasses (`MO/Documentation~/get-started/meta-vr-glasses.md`). |
| com.unity.xr.arfoundation | **6.6.2** | 6.7.1 (2026-10-28) | meta-openxr 2.7.0 fixes the deprecation warnings that appear with ARF 6.7 (`MO/CHANGELOG.md`). If we pick ARF 6.7, we must also pick meta-openxr 2.7.0. |
| com.unity.xr.hands | **1.9.0** | 1.10.0 (2026-10-26) | |
| com.unity.xr.interaction.toolkit | **3.6.1** | 3.7.0 (2026-10-26) | |
| com.unity.ai.inference (Sentis successor) | **2.6.1** | 2.6.1 (2026-04-02) | `com.unity.inference-engine` **does not exist**: the registry returns `"Package com.unity.inference-engine not found"`. The editor manifest marks `com.unity.sentis` as "no longer supported on this editor version. Use com.unity.ai.inference instead." |
| com.unity.localization | **1.5.13** | 1.5.13 | Depends on addressables 1.25.0+. |
| com.unity.ai.navigation | **2.0.15** | 2.0.16 (2026-10-07) | |
| com.unity.render-pipelines.universal | **17.6.0** (core package) | n/a | The registry "latest" (10.10.1) is meaningless here, because URP is versioned with the editor. |
| com.unity.xr.management | 4.7.0 | dist-tag says 4.6.1, but 4.7.0 was published 2026-07-07 | The registry dist-tag is stale. meta-openxr requires 4.7.0. |
| com.unity.xr.compositionlayers | 2.6.0 | | Passthrough is implemented as a composition layer. |
| com.unity.inputsystem | 1.20.0 | | |
| com.unity.xr.androidxr-openxr | 1.4.0 | 1.5.1 | Port target later (Android XR). The editor also bundles it. |

What is cached on this machine:
- `EDITOR/Editor/` already holds every recommended tarball: `com.unity.xr.meta-openxr-2.6.1.tgz`, `com.unity.xr.openxr-1.18.0.tgz`, `com.unity.xr.arfoundation-6.6.2.tgz`, `com.unity.xr.hands-1.9.0.tgz`, `com.unity.xr.interaction.toolkit-3.6.1.tgz`, `com.unity.ai.inference-2.6.1.tgz`, `com.unity.localization-1.5.13.tgz`, `com.unity.ai.navigation-2.0.15.tgz`, `com.unity.xr.compositionlayers-2.6.0.tgz`, `com.unity.xr.androidxr-openxr-1.4.0.tgz` and `com.unity.inputsystem-1.20.0.tgz`. Command: `ls EDITOR/Editor`.
- `~/Library/Unity/cache/packages/packages.unity.com` holds only old versions (arfoundation 5.x, openxr 1.9.1, XRI 2.5.2). They are irrelevant.
- Not cached: UniTask. It comes from OpenUPM or git and is out of scope here.

Meta SDK registry, for reference only: `curl https://npm.developer.oculus.com/com.meta.xr.mrutilitykit` shows latest 207.0.1 (2026-10-12), which depends on `com.meta.xr.sdk.core` 207.0.1.

---

## 2. Passthrough Camera Access (riskiest piece)

### 2.1 Platform facts (Meta)

Source: https://developers.meta.com/horizon/documentation/unity/unity-pca-overview/

- Requires **Horizon OS v74 or later** and a **Quest 3 or 3S**. Our v209 headset qualifies.
- Permissions: `horizonos.permission.HEADSET_CAMERA` covers the passthrough cameras only. `android.permission.CAMERA` covers passthrough plus the avatar cameras.
- Resolutions: 1280x960, 1280x1280 (v83+) and **1728x1728 (v209+)**. Don't hardcode sizes.
- Performance: 60 Hz, **20–40 ms capture latency**, ~1–2% GPU per camera stream, ~45 MB memory, YUV420.
- Passthrough must be enabled.
- Camera frames are **Device User Data** under Meta's Developer Data Use Policy (https://developers.meta.com/vr/policy/data-use/). The README and privacy note must say this, because we send frames to the Meta API.
- Known issue: after the first install following a fresh OS flash on v74, RGB channels may flip. A reboot fixes it.

### 2.2 Unity OpenXR: Meta image capture (primary)

Sources:
- Docs: https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.7/manual/features/camera/image-capture.html (same file as `MO/Documentation~/features/camera/image-capture.md`)
- Code: `MO/Runtime/Subsystems/Camera/MetaOpenXRCameraSubsystem.cs`

Setup:
- Turn on **Project Settings → XR Plug-in Management → OpenXR → Android → Meta Quest → Meta Quest: Camera (Passthrough) → gear → Camera Image Support**.
- The **minimum Android API level is 32 (12L)**. Both the docs note and `ARCameraFeature.cs` (`k_CameraMinSdkVersion = AndroidSdkVersions.AndroidApiLevel32`) say so.
- When Camera Image Support is on, the build adds these to the manifest (`MO/Editor/ModifyAndroidManifest.cs`):
  - `uses-feature android.hardware.camera`
  - `com.oculus.permission.USE_PASSTHROUGH_CAMERA`
  - `horizonos.permission.HEADSET_CAMERA`
  - `android.permission.CAMERA`
  Since 2.5.0 these are added only when we opt in (CHANGELOG).
- Scene setup: an `ARCameraManager` on the XR Origin camera, and a URP camera background of Solid Color with **alpha 0**. `ARCameraBackground` is not needed (`passthrough.md`).
  - **Enabling `ARCameraManager` enables passthrough. Disabling it disables passthrough.**

Verified CPU APIs, from `MO/Tests/Runtime/CodeSamples/CpuImageSamples.cs` and the subsystem source:

```csharp
var sub = cameraManager.subsystem as MetaOpenXRCameraSubsystem;
if (sub.TryAcquireLatestCpuImage(out XRCpuImage image)) { using (image) { ... } }      // mono = left camera
sub.TryAcquireLatestCpuImageForPosition(MetaOpenXRCameraSubsystem.CameraPosition.LeftEye, out XRCpuImage.Cinfo c);
new XRCpuImage(sub.cpuImageApi, c);                                                    // wrap Cinfo
sub.TryAcquireLatestStereoCpuImagePair(out MetaOpenXRCameraSubsystem.XRCpuImagePair p); // p.leftEyeImageCinfo / rightEyeImageCinfo
sub.TryGetIntrinsics(out XRCameraIntrinsics k) / sub.TryGetIntrinsicsForPosition(pos, out k); // focalLength, principalPoint, resolution (px)
sub.TryGetFrameForPosition(pos, XRCameraParams, out XRCameraFrame f);                 // timestamp, projection/display matrix
sub.SupportsStereoImageCapture(); sub.GetAvailableCameras();
MetaOpenXRCameraSubsystem.SetMaxStereoSyncAttempts(1..10);
```

- `ARCameraManager.TryAcquireLatestCpuImage(out XRCpuImage)` and `TryGetIntrinsics` exist in ARF 6.6.2 (`Runtime/ARFoundation/Camera/ARCameraManager.cs:267,316`).
- Conversion APIs, from ARF 6.6.2 `Runtime/ARSubsystems/Common/Textures/XRCpuImage/XRCpuImage.cs`:
  - `XRCpuImage.ConversionParams(image, TextureFormat, Transformation)`, where `Transformation` is `MirrorX` or `MirrorY`
  - `GetConvertedDataSize`
  - `Convert(ConversionParams, NativeSlice<byte>)`
  - `ConvertAsync(ConversionParams)`, which returns `AsyncConversion`
  - `image.timestamp`, `image.dimensions`, `image.format`
  - Use `ConvertAsync` to RGBA32 off the main thread. 2.6.1 made YUV→RGBA ~30% faster (CHANGELOG).
- `supportsCameraConfigurations = false` (subsystem descriptor), so **we cannot choose the resolution**. The resolution the subsystem delivers on v209 (1280x960 or 1728x1728) is **UNVERIFIED**. Read `image.dimensions` and downscale before upload.
- GPU path (`TryAcquireLatestGpuImage`, Vulkan only, acquire and release inside `RenderPipelineManager.begin/endCameraRendering`) is not needed for SAM upload. Skip it.
- Editor and Quest Link: the descriptor sets `supportsCameraImage = true` only under `UNITY_ANDROID && !UNITY_EDITOR`. **No CPU images in Play Mode or Link.** We need a mock `ICameraFrameSource` (a static JPEG) for editor tests.

### 2.3 Runtime permission flow (must get right)

From `MetaOpenXRCameraSubsystem.cs` (`Start()`):

```csharp
if (!cameraFeature.cameraImageSupportEnabled) state = SupportNotRequested;
else if (!Permission.HasUserAuthorizedPermission("horizonos.permission.HEADSET_CAMERA")) state = NoPermission;
else if (!NativeApi.UnityMetaQuest_InitializeCamera()) state = PlatformError; else state = Ready;
```

- The check runs **only in `Start()`**. If the user grants the permission after the subsystem started, every acquire keeps returning `false` until the subsystem restarts.
- The package never calls `RequestUserPermission` itself (grep for `RequestUserPermission` in `MO/Runtime` finds nothing).
- Pattern:
  - Ship the scene with `ARCameraManager` disabled.
  - Batch-request `HEADSET_CAMERA`, `com.oculus.permission.USE_SCENE` and `android.permission.RECORD_AUDIO` with `UnityEngine.Android.Permission.RequestUserPermissions(string[], PermissionCallbacks)`, the same call XRI's sample `Samples~/Starter Assets/Scripts/PermissionsManager.cs:121` uses.
  - Enable `ARCameraManager` on the grant callback.
  - The scene-data managers follow the same rule: Unity says to disable `ARPlaneManager`, `ARBoundingBoxManager`, `ARMeshManager` and `AROcclusionManager` until `USE_SCENE` is granted (`MO/Documentation~/get-started/scene-setup.md#permissions`, sample `MO/Tests/Runtime/CodeSamples/PermissionsCheck.cs`).
- Unity automatically requests `RECORD_AUDIO` on first `Microphone` use and `CAMERA` on first `WebCamTexture` use. Add `unityplayer.SkipPermissionsDialog` to the manifest to own the flow ourselves. Source: https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/developing/device-features-and-permissions/permissions-in-unity
- Because passthrough is off while `ARCameraManager` is disabled, the first seconds before the grant show the opaque/splash view. The **Passthrough Pre Splash Screen** option (`passthrough.md`) mitigates this. Whether disabling and re-enabling `ARCameraManager` causes a visible flicker is UNVERIFIED.

### 2.4 Fallbacks

**A. `WebCamTexture` (recommended fallback)**
- Meta's overview lists `WebCamTexture` and `WebCamDevice.availableResolutions` as the Unity access path.
- It needs no Meta SDK, so licensing stays clean.
- Pass an explicit resolution (e.g. 1280x960). A parameterless constructor picks the maximum (1728x1728 on v209). Meta warns about this.
- Camera pose comes from §2.5.
- Whether `WebCamTexture` and the meta-openxr camera subsystem can stream at the same time is **UNVERIFIED**. They likely contend for the same Camera2 device, so use one or the other and keep the choice behind an interface.

**B. Meta MRUK `PassthroughCameraAccess` (MRUK 207.0.1, `Core/Scripts/PassthroughCameraAccess.cs`)**
- Docs: https://developers.meta.com/horizon/documentation/unity/unity-pca-documentation
- API:
  - `GetTexture()`, `GetColors()` (returns `NativeArray<Color32>`)
  - `Intrinsics` {FocalLength, PrincipalPoint, SensorResolution, LensOffset}
  - `GetCameraPose()`, `ViewportPointToRay(Vector2, Pose?)`, `WorldToViewportPoint`
  - `Timestamp`, `CurrentResolution`, `RequestedResolution`, `GetSupportedResolutions`
- It depends on `com.meta.xr.sdk.core` (OVRPlugin), for example `OVRPlugin.GetSystemHeadsetType()`.
- **License:** MRUK's `LICENSE.md` and the samples repo `oculus-samples/Unity-PassthroughCameraApiSamples` (GitHub API `license` = NOASSERTION, content = "Oculus SDK License Agreement") are both under the **Oculus SDK License**, which is not OSI. Consequences:
  - Don't vendor or copy that code into our repo.
  - Users pull it from Meta's scoped registry and accept the license.
  - Document it in THIRD_PARTY.
- Whether the Meta XR Core SDK can coexist with `com.unity.xr.meta-openxr` in one project is **UNVERIFIED**. It adds a second XR stack, so use it only as a last resort.

### 2.5 Pixel / mask → 3D world point (for "look at" and "walk to")

meta-openxr gives **intrinsics only**: `XRCameraIntrinsics` with `focalLength`, `principalPoint` and `resolution` (ARF `XRCameraIntrinsics.cs`). There is **no camera-to-head extrinsic API**. A grep for `extrinsic|lens|pose` in `MO/Runtime/Subsystems/Camera` finds nothing.

**Extrinsics source:**
- Android Camera2 `CameraCharacteristics.LENS_POSE_TRANSLATION` and `LENS_POSE_ROTATION`, read via `AndroidJavaObject` (`getSystemService("camera")` → `getCameraCharacteristics(id)` → `get(key)`).
- Meta's older sample did this (`PassthroughCameraUtils.cs` at commit `90251538` of `oculus-samples/Unity-PassthroughCameraApiSamples`). Its comment: "LENS_POSE_TRANSLATION and LENS_POSE_ROTATION … are relative to the origin, so they can be cached".
- Its conversion:
  - `p_headFromCamera = (t0, t1, -t2)`
  - `q_headFromCamera = Inverse(new Quaternion(-r0, -r1, r2, r3))`
  - `worldFromCamera = worldFromHead * headFromCamera`, then rotation `*= Euler(180,0,0)`
- MRUK 207 does the same math (`PassthroughCameraAccess.cs:429,573-590`).
- Pick the camera id by the vendor tag `com.meta.extra_metadata.position` (0 = left, 1 = right), per the PCA overview.
- We reimplement this math from the Android Camera2 docs and do not copy Meta's code (license).
- That "head" in Camera2's frame equals Unity's XR Origin camera (centre-eye) pose under OpenXR is **UNVERIFIED**. Validate on device by projecting a known object.

**Ray from pixel:** this follows MRUK's `ViewportPointToLocalRay`, including the sensor crop.

```
dirCam = ((px - cx)/fx, (py - cy)/fy, 1)
rayWorld = new Ray(camPose.position, camPose.rotation * dirCam)
```

- When the delivered image is smaller than the sensor resolution, use the delivered image's intrinsics. meta-openxr returns intrinsics in image pixels: its `currentConfiguration` uses `cameraIntrinsics.resolution`.
- Image Y-flip: Camera2 has a top-left origin. Whether `XRCpuImage` needs `Transformation.MirrorX` for Unity textures is **UNVERIFIED**. Test once with a known marker.

**Head pose at capture time:**
- The image is 20–40 ms old.
- meta-openxr has no "pose at timestamp" API.
- Options:
  - Store the head pose when we acquire the image. Error is a few cm at 1 m for normal head motion. That is acceptable, because the companion UX asks the user to "hold the object up".
  - Or keep a ring buffer of `(Time, headPose)` and match `XRCpuImage.timestamp`. The **clock domain of the timestamp is UNVERIFIED**.

**Hit point:**
- Use `ARRaycastManager.Raycast(Ray, List<ARRaycastHit>, TrackableType.Depth)`.
- Meta's runtime supports world-based raycasts against **Depth**. AR Foundation's fallback adds `PlaneWithinPolygon`, `PlaneWithinBounds` and `BoundingBox` (`MO/Documentation~/features/raycasts.md`).
- The raycast feature also adds the `USE_SCENE` permission (`ModifyAndroidManifest.cs`).
- For a SAM mask: cast rays for the mask centroid plus a few interior samples, take the median hit distance, and use that point as the look-at or walk target. Project it onto the NavMesh with `NavMesh.SamplePosition`.

---

## 3. Meta VR Glasses (stated main target): what changes

Unity Manual (6000.6), https://docs.unity.com/en-us/engine/6000.6/manual/xr/configuring-project-for/meta-quest-develop/meta-vr-glasses:
- OpenXR, Unity 6.6+, IL2CPP/ARM64.
- "eyes and hands are the primary input… controllers aren't included".
- Look-and-pinch interaction.

Meta compare page (https://developers.meta.com/horizon/essentials/compare-devices):
- Passthrough: "Color stereoscopic (26 PPD) passthrough with autofocus and depth sensing".
- 120 Hz.
- "Snapdragon Reality Elite Platform".

Meta getting-started page:
- "There's no camera-based face tracking available to apps".
- **Neither page says whether PCA works on the glasses: UNVERIFIED.** The Quest 3 is our only confirmed PCA device.

Design implications:
- Hands are the primary input; controllers are a fallback only.
- Add eye-gaze targeting through the OpenXR Eye Gaze Interaction profile.

---

## 4. Scene understanding (OpenXR, meta-openxr)

All of it needs **Space Setup** completed on the device and **`com.oculus.permission.USE_SCENE`** granted at runtime. The manifest entry is added automatically when the Planes, Bounding Boxes, Meshing or Raycast features are enabled (`ModifyAndroidManifest.cs`).
- Meta's runtime **does not discover planes dynamically**. It returns the stored Scene Model (`planes.md`).
- If Space Setup is missing, prompt for it with `MetaOpenXRSessionSubsystem.TryRequestSceneCapture()`. The app pauses and resumes (`session.md`).

### 4.1 Planes (`ARPlaneManager`)
- Provider types:
  - `Xr Fb Scene` (default): tables, couches, wall art, and simplified walls, floor and ceiling.
  - `Xr Meta Spatial Entity Room Mesh`: higher-fidelity walls, floor and ceiling.
- `ARPlane.classifications` maps to these labels:
  - Table, Couch, **Floor**, Ceiling, WallFace, WallArt, DoorFrame, WindowFrame, InvisibleWallFace, InnerWallFace, Other
- `trackableId` **persists across sessions** within the same Space Setup.
- The plane subsystem only logs a warning when `USE_SCENE` is missing (`MetaOpenXRPlaneSubsystem.cs:72`).

### 4.2 Bounding boxes (`ARBoundingBoxManager`)
- Classifications: Couch, Table, Bed, Lamp, Plant, Screen, Storage, Other. Multiple labels per box are possible.
- IDs persist across sessions (`bounding-boxes.md`).
- Good for "Jolly, sit on the couch" or "go to the table".

### 4.3 Meshing (`ARMeshManager`)
- Returns the Space Setup room mesh (`meshing.md`).
- Per-face semantic classification for meshes is not documented in the package. Treat it as **UNVERIFIED**.

### 4.4 Walkable floor + "walk to the kitchen"
- **NavMesh:** `NavMeshSurface` from ai.navigation 2.0.15 (`Runtime/NavMeshSurface.cs`) provides:
  - `BuildNavMesh()` (synchronous)
  - `UpdateNavMesh(NavMeshData)`, which returns an `AsyncOperation`
  - the `useGeometry` setting (`NavMeshCollectGeometry.RenderMeshes` or `PhysicsColliders`)
- Recipe:
  1. Wait for the Floor `ARPlane` to appear.
  2. Give it a `MeshCollider` on a "NavGeometry" layer.
  3. Add each `ARBoundingBox` as a `BoxCollider` (or a `NavMeshObstacle` with carving).
  4. Set `useGeometry = PhysicsColliders` and `layerMask = NavGeometry`.
  5. Call `BuildNavMesh()` once at start, then `UpdateNavMesh` when trackables change.
  - Optional: use room-mesh walls as obstacles.
- **Rooms:** the Scene Model exposes plane and box labels only. There is **no "kitchen" or room-type label** (label tables above).
  - Option 1 (recommended):
    - The user says "this is the kitchen".
    - We create an `ARAnchor` at the user's position or gaze hit.
    - We persist it with `ARAnchorManager.TrySaveAnchorAsync`. The returned GUID equals the `trackableId` (`MO/Documentation~/features/anchors/anchors-feature.md`).
    - We store the name→GUID map locally and reload with `TryLoadAnchorsAsync`.
  - Option 2: have the LLM name the room from a camera frame, then save it the same way.
  - Space Setup usually covers one room. Whether multi-room Scene Models are surfaced through meta-openxr is **UNVERIFIED**. Outside the scanned area there is no NavMesh, so "walk to the kitchen" may need to degrade to "point toward the anchor".

---

## 5. Hands, input, microphone

- **XR Hands 1.9.0:**
  - Hand Tracking OpenXR feature.
  - **Meta Hand Tracking Aim** gives `MetaAimHand` with `pinchStrengthIndex`, `indexPressed`, `aimFlags` and an aim pose (`Documentation~/openxr-features/metahandtrackingaim.md`). The old `MetaHandTrackingAim` type is `[Obsolete]`.
  - Portable alternative: the OpenXR **Hand Interaction Profile** (`XR_EXT_hand_interaction`, `<HandInteraction>` layout; openxr 1.18 `Documentation~/features/handinteractionprofile.md`). Prefer it for Android XR and Pico.
- **Gestures:**
  - `XRHandShape` / `XRHandPose` ScriptableObjects (Runtime).
  - The `StaticHandGesture` component, with `gesturePerformed` / `gestureEnded`, ships **in the Gestures sample** (`Samples~/Gestures/Examples/Scripts/StaticHandGesture.cs`), not in Runtime.
  - The sample includes an **"Open Palm Up"** hand pose and a "Point At" pose.
  - Push-to-talk: hold palm-up, or hold a pinch on the aim device. Copy the sample script into our assembly; it is under the Unity Companion License.
- **XRI 3.6.1:**
  - Starter Assets `XR Origin (XR Rig).prefab` with `XRInputModalityManager` for automatic hand/controller switching (`Runtime/Inputs/XRInputModalityManager.cs`).
  - Poke and near-far interactors.
  - A `Permissions Manager.prefab`.
  - Add the **Oculus Touch Controller Profile** for the controller fallback. The Meta Quest feature's validation also asks for a controller profile (`MetaQuestFeature.cs:326`).
- **Microphone:**
  - `UnityEngine.Microphone`. Using it makes Unity add `RECORD_AUDIO` and ask on first use (Unity permissions doc above).
  - Query the sample rate with `Microphone.GetDeviceCaps`. The Quest's native mic rate is **UNVERIFIED**; resample to 16 kHz for STT.

---

## 6. Project settings checklist (Quest 3)

| Setting | Value | Source |
|---|---|---|
| Build profile | **Meta Quest** platform (Unity 6.1+) | `MO/Documentation~/get-started/project-settings.md` |
| XR Plug-in Management (Android) | OpenXR + Meta Quest feature group; disable ARCore | same |
| OpenXR features (Meta Quest group) | Meta Quest Support; Session; Camera (Passthrough) + **Camera Image Support**; Planes; Bounding Boxes; Meshing; Raycasts; Anchors; Occlusion (optional); Hand Tracking; Meta Hand Tracking Aim; Hand Interaction Profile; Oculus Touch Controller Profile; Foveated Rendering | package docs |
| Graphics API | **Vulkan only** | `graphics-settings.md`; required by symmetric projection, SpaceWarp and the GPU image path |
| Scripting backend / arch | **IL2CPP / ARM64** (Meta Quest profile default) | Unity Manual (glasses page) |
| Min API level | **32 (Android 12L)** for CPU camera images | `image-capture.md`, `ARCameraFeature.cs` |
| Application entry point | **GameActivity** | `MetaQuestFeature.cs:655` |
| Orientation | Landscape Left | `MetaQuestFeature.cs:666` |
| Stereo render mode | Single Pass Instanced / Multiview; Symmetric Projection on; Multiview Render Regions = Final Pass | `MetaQuestFeature.cs:422-604` |
| URP asset | HDR off, Terrain Holes off; Renderer: post-processing off, Intermediate Texture = Auto; **Render Graph** on (no compatibility mode); no SSAO | `graphics-settings.md`; `MetaQuestFeature.cs:374`; SpaceWarp prerequisites |
| Camera | Solid Color background, alpha 0 | `passthrough.md` |
| Foveated rendering | OpenXR Foveated Rendering feature with the SRP Foveation API (FFR on Quest 3; gaze-based on the glasses) | openxr `features/foveatedrendering.md` |
| Application SpaceWarp | Optional; needs URP 17.0.3+, Render Graph, Vulkan, and motion-vector support in shaders (the Jolly material must write motion vectors) | openxr `features/spacewarp/*` |
| Refresh rate | Target 72 Hz baseline, 90 Hz stretch (the display supports up to 120 Hz, per the Meta compare page). Enable the **Meta Quest: Display Utilities** feature, then call `XRDisplaySubsystem.TryGetSupportedDisplayRefreshRates` and `TryRequestDisplayRefreshRate` (extension methods in `MetaOpenXRDisplaySubsystemExtensions`). | `MO/Documentation~/features/display-utilities.md`, `MO/Tests/Runtime/CodeSamples/MetaQuestDisplayUtilitiesSample.cs` |
| Internet | Keep **Force Remove Internet Permission OFF** (Meta Quest Support settings), and set Player → Internet Access = Require | openxr `features/metaquest.md` |
| Space Setup | Prompt via `TryRequestSceneCapture()` when no Floor plane appears | `session.md` |

---

## 7. Top risks (ranked)

1. **Camera extrinsics are not in meta-openxr.** The Camera2 `LENS_POSE_*` via JNI path is proven by Meta's sample, but the frame convention under OpenXR (head = centre-eye?) is UNVERIFIED. Spike it on day 1 by projecting a marker.
2. **The permission timing bug class.** The camera subsystem only checks the permission in `Start()`. A wrong order gives silent `false` results (one warning per method). Gate `ARCameraManager` on the grant.
3. **No camera frames in the Editor or over Link.** We need an injectable frame source for tests and iteration. Every PCA test has to run on the headset.
4. **Delivered resolution and orientation are unknown on v209** (no camera configurations; 1728² is possible). Read `dimensions` and downscale before SAM upload; determine the mirror flag empirically.
5. **Scene data depends on the user** (Space Setup + `USE_SCENE`). There are no room labels, so "walk to the kitchen" needs taught anchors. A NavMesh exists only inside the scanned room.
6. **Meta VR Glasses PCA support is UNVERIFIED.** Keep the camera behind an interface. On the glasses, hands and eyes are the only guaranteed input.
7. **License:** the PCA fallback through MRUK, and Meta's samples, are under the Oculus SDK License (non-OSI). Don't vendor them. `WebCamTexture` is the clean fallback.
8. **Version drift:** a newer set (1.19 / 2.7 / 6.7.1) was released in the last week. Upgrade it as a unit, if at all. ARF 6.7 together with meta-openxr 2.6.1 produces deprecation warnings.
