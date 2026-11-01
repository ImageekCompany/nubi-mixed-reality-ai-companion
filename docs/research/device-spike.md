# Device spike: Quest 3, IL2CPP, critic risks R1 / R2 / R4 (transport) / R5 (mic)

Date: 2026-11-01, 01:10–01:40 CET. Device: Quest 3 `2G0YC1ZF7P090N`, Horizon OS v209 (`ro.vros.build.version=209`, Android 14 / API 34). Editor 6000.6.6f1, IL2CPP ARM64, Vulkan.
Nobody wore the headset. Everything below is measured from logcat, from `spike_results.json` pulled with adb, and from `aapt dump` of the APK.
Raw outputs (camera frames, mic audio, results JSON) are in `docs/research/samples/device/run{1,2}/`. That folder is **git-ignored**, because it holds captures from the developer's home.

Spike code: `JollyXR/Assets/Spikes/` (asmdef `Jolly.Spikes`, to be deleted later). Both spike asmdefs are define-constrained to `JOLLY_SPIKES`, so the spike code is not compiled into normal builds; add the symbol to the Android scripting defines for a spike build and remove it afterwards. The runner is `DeviceSpike.cs`. It needs no input and writes `persistentDataPath/spike_results.json`.
Build tooling that stays: `JollyXR/Assets/Jolly/Editor/Build/` (`QuestProjectSetup.Apply`, `QuestBuild.BuildApk`, `NubiManifestRequirements`) plus `tools/device/*.sh`.

## 1. Results

| # | Item | Status | Measured |
|---|---|---|---|
| a | Microphone (R5 mic part) | **PASS; intermittent stall UNVERIFIED (no artifact, see §7)** | `Microphone.devices` = `["Android audio input"]`. `GetDeviceCaps` = 16000–48000. The clip is 48 kHz mono. First sample arrives 162–198 ms after `Microphone.Start`. Quiet-room RMS is −82…−90 dBFS, so data is real, not zeros. **1 of 6 runs stalled**: the first launch after a fresh `adb install`, while the app was still loading (app load time 8.6 s). That run took 1090 ms to the first sample, delivered one 2880-sample buffer, then nothing, all zeros. The audio HAL logged normal `start`, routed to `handset-mic`. |
| b | Passthrough CPU image (R1) | **PASS (pipeline). Content and orientation BLOCKED: dark room** | `XRCpuImage` **1728×1728 `AndroidYuv420_888`**, 3 planes: Y stride 1728 / pixelStride 1, and UV pixelStride 2, which is interleaved semi-planar. The first image arrives 15–16 ms after the subsystem runs. Right eye via `TryAcquireLatestCpuImageForPosition(RightEye)` works (same size). `SupportsStereoImageCapture()=true`, `GetAvailableCameras()=Both`. `ConvertAsync`→RGBA32 takes **74–76 ms at full res**. A 640² nearest-neighbour downsample (`outputDimensions`) takes **12–14 ms**, then `EncodeToJPG`. Mean luma is **2.2/255** in both cameras, also after 2 s. The tracking service reported `3DoF`, `MeanIr=1.4` (lights off), so the frames are black and the None-vs-MirrorY orientation could not be judged. |
| b | Intrinsics | **PASS** | meta-openxr `TryGetIntrinsics`: left fx=fy=1172.536, cx=869.562, cy=861.484. Right fx=fy=1170.136, cx=859.486, cy=859.132. Both at 1728². These are **identical** to Camera2 `LENS_INTRINSIC_CALIBRATION` (skew 0) of camera ids 50/51. |
| b | Extrinsics via Camera2 JNI | **PASS (available). Frame convention UNVERIFIED** | `getCameraIdList` = `1, 50, 51`. **50 = left** (`com.meta.extra_metadata.position=0`) and **51 = right** (`=1`). Both are 1728², `LENS_FACING=1`, **`LENS_POSE_REFERENCE=1` (GYROSCOPE)**. Left `LENS_POSE_TRANSLATION` = (−0.03229, −0.01753, −0.06316) m, `ROTATION` (x,y,z,w) = (−0.99527, 0.00269, −0.00406, 0.09704). Right = (0.03115, −0.01734, −0.06308) m, (−0.99510, 0.01005, 0.00236, 0.09831). `LENS_DISTORTION` = null. Id `1` is 1600×1200 with no pose (speculation: the avatar camera). Vendor keys: `com.meta.extra_metadata.{camera_source, position, camera_name, camera_thumbnail}`. |
| b | Timestamp clock domain | **PASS** | `XRCpuImage.timestamp` is **CLOCK_MONOTONIC in nanoseconds**. It matches `SystemClock.uptimeMillis` (`9275.7595 s` vs `9275.806 s`), not `elapsedRealtimeNanos` (boot time 347 354 s). Frame age at acquire is **31–46 ms**, consistent with Meta's 20–40 ms capture latency. |
| c | SAM 3.1 from device | **PASS (on CC-BY test image)** | The camera frame was dark, so the app used the pushed `test_image.jpg` (`samples/pen_640.jpg`). Non-streaming `/v1/responses`, `one_bit`. Prompt `"object"` returned **0 objects** in 1110 ms (cold TLS). Spark's noun `"Blue ballpoint pen"` returned **1 object: the blue cap only** (box 369,214–561,387, c=0.64) in **416 ms** (warm connection). The research run with `"pen"` returned the body. The mask (`!` encoding, 125 chars) decoded on the Mac with the research decoder: 193×174, coverage 0.351, correct cap shape. |
| c | Spark vision from device | **PASS (on CC-BY test image)** | `muse-spark-1.3` (standard), `reasoning_effort:minimal`, non-streaming: **1621 ms**, 404 prompt / 135 completion tokens (105 reasoning). Reply: "Blue ballpoint pen / It is a writing tool used by removing the cap and writing with the tip." |
| d | ASR realtime WebSocket under IL2CPP (R4 transport) | **PASS** | `System.Net.WebSockets.ClientWebSocket` works on Quest IL2CPP. TLS connect takes **469–652 ms cold and 152 ms warm** (second session, same process). Handshake to server ack takes **1.0–2.4 s**. The server buffers audio sent meanwhile, then catches up: partials then arrive every 20–500 ms. The final arrives **166–208 ms after `endStream`** (208 ms in the §7 re-run), then close `1000 "No more transcript segments"`. Wit-synthesised input "Hi, I am Nubi…" came back as "Hi, I am **Newbie**. Show me something and I will tell you what it is." The mic session (silence) returned `final:""`. |
| e | Wit TTS raw HTTP (R2) | **PASS** | `POST https://api.wit.ai/synthesize?v=20250213`, `Accept: audio/raw`, voice `Charlie`, **client token**. **TTFB 465–800 ms on device** (800 ms in the §7 re-run) (Mac: 341 ms). Playback through a streaming `AudioClip` (PCMReaderCallback) starts 11–13 ms after the first byte. A 4.34 s line (64 chars, 208 416 B PCM16 24 kHz mono) was fully received in 574–624 ms, in 14–16 chunks. `/voices`: en_US 23, en_CA 2, en_GB 5. **No Polish voice.** |
| – | Passthrough + rendering | **72 fps PASS; passthrough UNVERIFIED (conflicting logs, see §7)** | The compositor logs `PassthroughApiManager … PT is: ON` while `ARCameraManager` is enabled. VrApi reports a steady **72/72 FPS**, App GPU ≈2.2 ms, GPU 24 %, CPU 13 %, 3.5 GB free, 43 °C (on charger). `adb exec-out screencap` returns a black frame for this immersive app, so it is not usable for verification. |

## 2. Findings that change the plan

1. **Camera extrinsics are relative to the gyroscope**, not to a "head" or centre eye (`LENS_POSE_REFERENCE_GYROSCOPE`). The IMU→OpenXR centre-eye offset is unknown. MRUK and Meta's sample treat the pose as head-relative (xr-stack §2.5). A marker test with a person in the headset must settle this before "look at this" and "walk to" can be trusted. Left and right are mirror images at ±3.2 cm, −1.75 cm, −6.3 cm, with a ~169° rotation around X. That rotation is the Camera2 to OpenXR axis flip handled by the sample's `(−r0, −r1, r2, r3)` conversion.
2. **Do not gate on `ARCameraManager.permissionGranted`**. It reports `false` while frames flow. Gate on `Permission.HasUserAuthorizedPermission("horizonos.permission.HEADSET_CAMERA")`.
3. **Delivered resolution is 1728² on v209**, as predicted. Full-res conversion costs 75 ms, so convert straight to ≤640 px with `outputDimensions`, then JPEG (≈26 ms total). Nearest-neighbour aliasing is acceptable for SAM/Spark; a GPU blit is the upgrade path.
4. **SAM prompt wording matters.** A colour-qualified phrase from Spark segmented only the matching part (the blue cap). Ask Spark for a plain head noun ("pen") plus a separate attribute, or send SAM the head noun. A generic `"object"` returns nothing.
5. **ASR session spin-up costs 1–2.4 s before the server's ack.** For push-to-talk, open the WebSocket and send the handshake when the pinch *starts* (or when a hand is raised), not on release. The final after `endStream` is ~170 ms once the session is warm. "Nubi" is heard as "Newbie": normalise it client-side, and don't use `keywords` biasing (critic C5).
6. **Mic start can stall on a cold launch.** Start the microphone only after the XR session is focused and frames are presenting. Add a watchdog: if `Microphone.GetPosition` does not advance for 500 ms, call `End` + `Start` again.
7. **Quest's logcat main buffer is 256 KiB and rotates in about a minute.** Unity lines disappear. The scripts set `adb logcat -G 16M`. Results are also written to `spike_results.json`, which is the reliable channel.
8. **The OpenXR Foveated Rendering feature requests `EYE_TRACKING` permissions at every startup** (`OpenXRPermissionSettings.ApplyPermissionSettings`, openxr 1.18.0). This causes a ~70 ms pause/resume blip even though the permission is not in the manifest. It is harmless, but it is a focus change at boot.
9. **Eye-tracked foveation + the Quest Pro target makes the manifest *require* `oculus.software.eye_tracking`**, which Quest 3 does not have. Setup forces `FoveatedRenderingFeature.useEyeTracking=false`. Unity also adds `BLUETOOTH` for `Microphone`; `NubiManifestRequirements` removes it (Meta cert rejects it).
10. `HandTracking.featureId` (obsolete) equals `MicrosoftHandInteraction.featureId`. Enabling by that id silently turned on the HoloLens profile. Setup uses `featureId2` and forces the Microsoft profile off.
11. `unityplayer.SkipPermissionsDialog` is dead in Unity 6.6: no reference in the Android player jars or `libunity.so` (grep), so it is not added to the manifest.

## 3. Build settings (verified from `ProjectSettings.asset`, the URP asset and `aapt dump badging`)

| Setting | Value |
|---|---|
| Package / company / product | `com.nubi.companion` / Nubi Open Source / Nubi |
| Backend / arch / stripping | IL2CPP / ARM64 only / Minimal |
| Min / target SDK | 32 / **34**. 32 is the meta-openxr camera minimum. The editor-bundled SDK only has platforms 34/36/37, and a target of 32 fails Gradle with an unaccepted-licence download. 34 is the device's API level. Horizon Store target-API policy: UNVERIFIED. |
| Graphics / colour | Vulkan only / Linear. URP `NubiURP_Quest.asset`: MSAA 4×, HDR off, depth/opaque textures off, SRP batcher on, on every quality level |
| Entry / orientation / input | GameActivity / Landscape Left / Input System only (`activeInputHandler: 1`) |
| XR | OpenXR loader for Android, init on start. Features: Meta Quest Support (Quest 3/3S targets), Meta Session, Camera (+ **Camera Image Support**), Planes, Raycasts, Anchors, Display Utilities, Hand Tracking Subsystem, Meta Hand Tracking Aim, Hand Interaction Profile, Oculus Touch Profile, Foveated Rendering (fixed). Render mode Single Pass Instanced; latency mode Prioritize Input Polling. OpenXR project validation: **0 errors, 0 warnings**. |
| Manifest | `INTERNET`, `RECORD_AUDIO`, `MODIFY_AUDIO_SETTINGS`, `HAND_TRACKING`, `OPENXR(_SYSTEM)`, `HEADSET_CAMERA`, `USE_SCENE`, `USE_PASSTHROUGH_CAMERA`, `CAMERA`, `USE_ANCHOR_API`, `IMPORT_EXPORT_IOT_MAP_DATA`. Required feature `com.oculus.feature.PASSTHROUGH`. No `BLUETOOTH`, no eye-tracking requirement. `com.oculus.supportedDevices` set. |
| Packages | Editor-bundled XR set per xr-stack.md: openxr 1.18.0, meta-openxr 2.6.1, arfoundation 6.6.2, hands 1.9.0, XRI 3.6.1, management 4.7.0, ai.navigation 2.0.15, localization 1.5.13, inputsystem 1.20.0, URP 17.6.0, test-framework 1.8.0, glTFast 6.14.1, newtonsoft-json 3.2.2, ugui 2.6.0. UniTask 2.5.11 from OpenUPM. Unused default modules were removed. |

`QuestProjectSetup.Apply` is idempotent: a re-run logs no changes and reports 0 validation issues. (PARTIAL: the re-run still reimported `OpenXR Package Settings.asset`; see §7.)

## 4. Commands (also saved as `tools/device/*.sh`)

```bash
tools/device/build.sh                       # setup (session 1) → [NUBI_PRE_BUILD_METHOD] → BuildApk (session 2); logs in JollyXR/Logs/
NUBI_PRE_BUILD_METHOD=Jolly.Spikes.Editor.DeviceSpikeSceneBuilder.Build tools/device/build.sh   # regenerate the spike scene first
TMPDIR=<scratch> tools/device/push-secrets.sh  # .env → secrets.json (META_API_KEY, WIT_AI_CLIENT_TOKEN, WIT_AI_APP_ID) → /sdcard/Android/data/com.nubi.companion/files/
NUBI_HEADLESS=1 tools/device/install-run.sh # install -r -g, pm grant ×3, wake + prox_close + guardian_pause, launch
NUBI_PAID=1 NUBI_HEADLESS=1 NUBI_SKIP_INSTALL=1 tools/device/install-run.sh   # arm one vision+SAM run (~$0.01)
tools/device/logcat.sh NubiSpike --dump     # or follow; sets a 16 MiB buffer
tools/device/install-run.sh --restore-headset   # automation_disable + guardian_pause 0
adb pull /sdcard/Android/data/com.nubi.companion/files/spike_results.json
```

- Active Input Handling only takes effect after an editor restart, so setup and build run in **separate** editor sessions.
- Unity kills the adb server during a build ("Killing ADB server"). Run device commands after the build, not in parallel.
- `adb push` into `Android/data/<pkg>/files` works from the shell, and the app reads the file (`secrets.json loaded: META_API_KEY=True`).
- The Wit *server* token is never pushed; the client token is enough for `/synthesize`.

## 5. Needs a human

1. **Lights on, object in front of the headset**, then `NUBI_PAID=1 NUBI_HEADLESS=1 NUBI_SKIP_INSTALL=1 tools/device/install-run.sh`. This settles the orientation question (`cam_640_none.jpg` vs `cam_640_mirrory.jpg`; expectation: MirrorY is upright, because `Texture2D` row 0 is the bottom) and runs SAM and Spark on a real room frame.
2. **Extrinsics marker test** (needs someone wearing the headset): put a printed marker at a known spot, project the SAM mask centroid through the left-camera pose plus the head pose, and check the error at 1 m against the critic R1 pass bar (≤3 cm). This determines how the gyroscope-relative pose maps to the OpenXR centre eye.
   - Run it once while holding the head still and once while turning it slowly (~30°/s). The still run sets `gyroToHead` (`MetaPassthroughFrameSource` constructor). The error that appears only while turning is head-pose timing: OpenXR poses are predicted for display time, so tune `headPoseLeadSeconds` (`XrUserPoseProvider.Construct`, default 0; expect 0.02–0.04 s) until the turning error matches the still one.
3. **Runtime permission dialogs.** If `HEADSET_CAMERA` or `RECORD_AUDIO` is not granted, Horizon shows the system dialog, and Unity pauses (`PlayerLoop` stops, so in-app timeouts do not advance) until a person answers. Dev and CI use `adb install -g` / `pm grant`; both work for all three permissions. A tester must click through the first-launch dialogs once on a clean install.
4. **Space Setup** for planes/NavMesh: not exercised. `USE_SCENE` is granted; no plane test was run.
5. **Headless automation hooks.**
   - `am broadcast -a com.oculus.vrpowermanager.prox_close` worked: wakefulness went from Asleep to Awake.
   - `setprop debug.oculus.guardian_pause 1` was needed because "Launch is blocked because: a Guardian dialog is currently showing".
   - Both are developer-mode behaviours with no Meta doc found in this pass (UNVERIFIED as documented API). Both were **reverted** at the end of the session: `automation_disable`, `guardian_pause=0`.

## 6. Open risks / next steps

- R1: the gyroscope-relative extrinsics need the marker test. Orientation and real-frame SAM quality wait on light.
- R5: the mic cold-start stall needs the start-after-focus pattern and the watchdog. AEC and barge-in (rest of R5) were not tested.
- R4: transport is done. The end-to-end latency budget still needs the pinch-start pre-connect. Measure p50 with a person speaking.
- The spike's vision/SAM run on device was against a test image. `muse-spark-1.3` standard was used, per ToS §6.2.
- API spend for this spike: ≈ $0.012 (`docs/api-spend.md`).

## 7. Verification (independent re-check, 2026-11-01 01:39–01:45 CET)

Method: read the pulled artifacts in `samples/device/run1`, `run2` (JSON, JPEGs, WAV headers, SAM mask PNG), `JollyXR/Logs/{setup,build}.log`, `aapt dump badging/xmltree` of `JollyXR/Builds/Android/Nubi.apk` (01:34 build), and ran one fresh cycle without rebuilding: `push-secrets.sh` → `NUBI_HEADLESS=1 install-run.sh` (install -r -g, 3× pm grant, launch) → logcat dump → pull. Outputs in `samples/device/run3-verify/` (`spike_results.json`, `logcat_full.txt`; git-ignored). No paid vision/SAM call; ASR ×2 (~7.4 s audio). `--restore-headset` run afterwards (`guardian_pause` reads back `0`), app force-stopped.

**Only runs 1, 2 and run3-verify have artifacts.** Runs 3–6 of the spike exist only as a ledger row; nothing on disk backs them.

| Claim | Verdict | Evidence |
|---|---|---|
| Scripts work end-to-end headless | **VERIFIED** | run3-verify: install `Success`, 3 grants, launch, results file after ~28 s, `DONE` at t=21.8 s. |
| Mic: 48 kHz mono, first sample 162–198 ms, −82…−90 dBFS, not zeros | **VERIFIED** (3 runs) | run1 198 ms / −83.8; run2 184 ms / −89.3; run3 181 ms / −82.4; `allZero:false`. `mic.wav` header 48000 Hz, 1 ch, 16-bit, 3.04 s. The "162 ms" low end has no artifact. |
| Mic cold-start stall (1 of 6, 1090 ms, one 2880-sample buffer) | **UNVERIFIED** | No JSON, log or audio of that run was kept. Not reproduced in run3 (but run3 was `install -r`, not a fresh install). Keep the watchdog anyway; it is cheap. |
| CPU image 1728² YUV420_888, 3 planes, first image 14–16 ms, right eye works | **VERIFIED** | run1/2/3 JSON (`msToFirstImage` 16/15/14, `rightEye` 1728² in run2/3). |
| Convert full 72–76 ms / 640² 12–14 ms | **VERIFIED** | run1 74/12, run2 76/14, run3 72/13. |
| Frames black (dark room) | **VERIFIED** | `meanLuma` 2.2 (run2), 2.18 (run3); `cam_640_mirrory.jpg` viewed: uniformly black. Logcat shows `MeanIr=1.4` and tracking in **3DoF** (`6DoF acquire wait expired`), so the head pose in these runs is not meaningful either. |
| Orientation (None vs MirrorY) | **BLOCKED** | as stated; needs light. |
| Intrinsics == Camera2 calibration | **VERIFIED** | run1/2/3: `intrinsics_LeftEye` fx 1172.53577, cx 869.5621, cy 861.484 equal camera 50 `intrinsicCalibration`; right equals camera 51. |
| Extrinsics values, ids 50/51, `poseReference=1` | **VERIFIED** (values). Mapping to head/centre-eye **UNVERIFIED** | JSON values match the table. Interpretation still needs the marker test. |
| Timestamp = CLOCK_MONOTONIC ns, age 31–46 ms | **VERIFIED** | run2: ts 9275.7595 s vs uptime 9275.806 (46 ms), after 2 s 9277.9196 vs 9277.951 (31 ms); run3: 38 ms and 39 ms. Boot-time clock is ~348 400 s, so not elapsedRealtime. run1 field `monotonicNowSec` is mislabelled boottime. |
| `ARCameraManager.permissionGranted=false` while frames flow | **VERIFIED** | `permissionGranted:false` + `acquired:true` in all 3 runs, with Android permissions `true`. |
| Spark vision 1621 ms, 404/135 tokens, "Blue ballpoint pen" | **VERIFIED** (1 sample) | run2 `vision` block. Single call; not a latency distribution. |
| SAM "object" → 0 objects 1110 ms; noun → cap only 416 ms | **VERIFIED** (1 sample each) | run2 `sam_generic` (empty), `sam_noun` box 369,214–561,387 c=0.64; `sam_noun.txt` holds the full mask string; `sam_noun_mask_obj0.png` viewed: cap with clip, matches the cap in `pen_640.jpg`. "Cold vs warm" attribution is inferred from call order, not measured. |
| ASR WS under IL2CPP: connect 469–652 ms cold / 150 ms warm; ack 1.0–2.4 s; final ~170 ms after endStream; "Newbie" | **VERIFIED**, ranges widened | run1 652 / ack 2277 / final 166; run2 469 / 2916 / 181, warm 152 / 1207; run3 510 / 2041 / 208, warm 150 / 1178. Ack after **connect** is 1.0–2.4 s, but measured from session start it reaches **2.9 s**. Final after endStream: **166–208 ms** (mic/silence session 305–334 ms). "Newbie" in all 3. Input was Wit TTS audio, not a human voice. |
| Wit TTS: TTFB 465–543 ms, playback +11–13 ms, 4.34 s line in ~600 ms | **VERIFIED, range widened** | run1 498/511/582, run2 543/554/624, run3 **800**/812/896. Use **TTFB 0.5–0.8 s** for budgets. `tts.wav` header 24 kHz mono 16-bit 4.342 s. run3 log: `tts playback finished, isPlaying=True` (speculation: the streaming clip keeps playing silence past the end; check before relying on `isPlaying` for turn-taking). "465 ms" low end has no artifact. |
| No Polish voice | not re-checked | Wit `/voices` output not kept as an artifact. |
| Passthrough ON | **UNVERIFIED (conflicting logs)** | run3: app gets `XR_META_passthrough_layer_resumed_event`, `PassthroughApiManager … PT is: ON numLayers: 0`, but `MRSS … PT is: OFF`, `PassthroughInCompositor start/stop request 1->0` and the passthrough camera client stops. With tracking in 3DoF in the dark this is expected to be degraded. Only a person in the headset can confirm passthrough is visible. |
| 72 fps, GPU ~24 % | **VERIFIED** | run3 VrApi: `FPS=73/72`, `GPU%=0.23`, `CPU%=0.13–0.15`, App 2.1–2.3 ms, Free 3523 MB, 40 °C. A near-empty scene, so not a perf budget for the real app. |
| Manifest: no BLUETOOTH, no eye-tracking requirement, PASSTHROUGH required, min 32 / target 34, arm64 only | **VERIFIED** | `aapt dump badging`: `sdkVersion:'32'`, `targetSdkVersion:'34'`, `native-code: 'arm64-v8a'`, permission list as §3, `uses-feature: com.oculus.feature.PASSTHROUGH` required, `oculus.software.handtracking` not required, no BLUETOOTH, no eye-tracking feature. `install-location: preferExternal` (Unity default; harmless). |
| `HandTracking.featureId2` / Microsoft profile collision | **VERIFIED** | `com.unity.xr.hands` CHANGELOG: `featureId` deprecated for colliding with Microsoft Hand Interaction; setup uses `featureId2`. |
| `useEyeTracking=false` | **VERIFIED in code** | `FoveatedRenderingFeature.useEyeTracking` (serialized, openxr package source); set at `QuestProjectSetup.cs:252`. Effect visible in the manifest (no eye-tracking requirement). |
| Setup idempotent, 0 errors / 0 warnings | **PARTIAL** | `setup.log`: `OpenXR validation: 0 error(s), 0 warning(s)`, no change lines. But the same run reimported `Assets/XR/Settings/OpenXR Package Settings.asset`, so something touched it (possibly a no-op save). Confirm with `git diff` after a second run once JollyXR is committed. |
| Build | **VERIFIED** | `build.log`: `[QuestBuild] Result=Succeeded … errors=0 warnings=2`. The 2 warnings were not identified. Unity log has `Killing ADB` (1×). |
| `SkipPermissionsDialog` dead in 6.6 | not re-checked | grep-of-binaries claim; low impact. |
| `guardian_pause`, `prox_close` needed/worked | **VERIFIED as working**, undocumented | run3 launched with them; restore leaves `guardian_pause=0`. |

**Secrets / git hygiene (VERIFIED):** `git check-ignore` matches `docs/research/samples/device/**` (incl. run3-verify), `JollyXR/Builds/…/Nubi.apk`, `.env`, `JollyXR/.utmp/`. No device capture is tracked (`git ls-files docs/research/samples/device` is empty). A byte scan for the first 8 chars of all four `.env` values over the whole project tree (Library, Builds, logs, samples included; `.env` excluded) and over `git log --all -p` found **0 hits**. The APK contains no `secret*`/`.env` entry.
