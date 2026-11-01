# 0003. Passthrough camera access through the meta-openxr camera subsystem, with Camera2 extrinsics

- Status: accepted (pending on-device spike R1)
- Date: 2026-11-01

## Context
SAM needs real camera frames (requirement 11). meta-openxr provides `MetaOpenXRCameraSubsystem.TryAcquireLatestCpuImage` and `XRCpuImage.ConvertAsync`, plus intrinsics. It does **not** provide the lens pose (`xr-stack.md` §2.5). Its permission check runs only in `Start()` (§2.3).

## Decision
- `MetaPassthroughFrameSource` (in `Jolly.XR.MetaQuest`):
  1. Ship the scene with `ARCameraManager` **and** every scene manager (`ARPlaneManager`, `ARBoundingBoxManager`, `ARRaycastManager`) disabled.
  2. Batch-request `horizonos.permission.HEADSET_CAMERA`, `com.oculus.permission.USE_SCENE` and `android.permission.RECORD_AUDIO` with `Permission.RequestUserPermissions(string[], PermissionCallbacks)`.
  3. Enable `ARCameraManager` once the callback returns, **whatever the result.** The camera subsystem's `Start()` creates the passthrough composition layer and only then checks the camera permission (`MetaOpenXRCameraSubsystem.cs:438-466`), so a denied camera still gets passthrough and reports `NoPermission`. If the permission is granted later, toggle `enabled` false/true to re-run `Start()`; the `NoPermission` state is latched until then.
  4. Enable the scene managers only after `USE_SCENE` is granted. Their subsystems otherwise start, log errors, and (for raycasts) latch `XR_ERROR_PERMISSION_INSUFFICIENT` (`MetaOpenXRRaycastSubsystem.cs:72-90`; planes `:69-76`, boxes `:32-38` of their subsystems).
  5. On `Capture(maxSide)`: `TryAcquireLatestCpuImage`, then `ConvertAsync` to RGBA32 with `ConversionParams.outputDimensions` at the target size and the mirror `transformation`, into a reused `NativeArray`, then return the frame with scaled intrinsics and the head pose at acquisition.
- **Extrinsics:** read `CameraCharacteristics.LENS_POSE_TRANSLATION` and `LENS_POSE_ROTATION` once via `AndroidJavaObject` (Camera2) and cache them. The coordinate conversion is reimplemented from the Android docs; no Meta sample code is copied.
- The world point comes from `ARRaycastManager.Raycast(Ray, hits, TrackableType.Depth)`; the meta-openxr provider supports only `Depth` (`MetaOpenXRRaycastSubsystem.cs:136,232`). Plane and box hits come from `Physics.Raycast` on their colliders.
- **Fallbacks behind `ICameraFrameSource`:**
  1. `WebCamTexture` with an explicit 1280×960 resolution.
  2. MRUK `PassthroughCameraAccess`, on a throwaway branch only, never committed.

## Consequences
- Every PCA test runs on the headset. The Editor uses `EditorStillFrameSource` (`Jolly.XR`).
- Denying the camera keeps passthrough; vision tools return `camera_unavailable`.
- These items are UNVERIFIED until R1 passes: the frame convention (Camera2 head versus the OpenXR centre eye), the mirror flag, the delivered resolution on v209, and the timestamp clock domain. The R1 pass criterion is a sphere placed within 3 cm at 1 m.
- The minimum Android API level is 32. The manifest must not use Force Remove Internet.

## Alternatives
- **MRUK PCA:** previews over Link and gives extrinsics for free, but it is under the Oculus SDK License and needs OVRPlugin.
- **`WebCamTexture` only:** clean licence, but there is no intrinsics API and it may contend with the subsystem for the camera.
