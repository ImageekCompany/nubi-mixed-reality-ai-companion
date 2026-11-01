# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Each work package appends bullets under its own heading only (`docs/scope.md` §3).

## [Unreleased]

### WP0
- Packages: `com.unity.animation.rigging` 6.6.0, uLipSync pinned to `ece3272` (git URL, ADR-0007) and `com.unity.timeline` 6.6.0, which uLipSync needs but does not declare. Existing pins unchanged.
- Every assembly of architecture §2 as an asmdef with a `-warnaserror+` `csc.rsp` (proved honoured on 6000.6.6f1: a deliberate CS0219 failed compilation), plus a `link.xml` stub per runtime assembly.
- `Jolly.Contracts` copied from `docs/contracts.md` §1–§8.1, one file per section; it compiles unchanged.
- `SpscFloatRing`: lock-free single-producer/single-consumer sample ring for the audio thread.
- `Jolly.Testing` with the shared fakes `FakeClock`, `InMemorySecrets`, `FakeRandom` and `RecordingLog`.
- Asmdef-graph test that pins the allowed references, `csc.rsp` presence, `autoReferenced:false` on runtime assemblies and test-only define constraints.
- `tools/ci/test.sh` (EditMode + PlayMode in batchmode, results XML parsed, compiler diagnostics and banned-API greps fail the run) and `tools/ci/build.sh` (IL2CPP APK via `QuestBuild.BuildApk`).
- `Scenes/Main.unity`: XRI Starter Assets XR Origin (hand interaction profile, locomotion off), AR Session, passthrough camera (`ARCameraManager`, transparent clear), directional light and an empty `JollyBootstrap`; it is the only build scene.
- `.editorconfig`.

### WP1

### WP2

### WP3

### WP4

### WP5
- `CameraMath` (Core/Spatial): pinhole pixel ↔ world ray with a top-left pixel origin and a Y-up Unity camera pose.
- Camera capture: `CameraFrameSlot` (one reused native buffer, one outstanding frame, release from any thread), `EditorStillFrameSource` (fixture image with Quest 3 camera FOV, top row first), `MetaPassthroughFrameSource` (left lens, `ConvertAsync` straight to the target size, head pose interpolated at the image's CLOCK_MONOTONIC timestamp × Camera2 lens pose) and `Camera2Extrinsics` (JNI, by `com.meta.extra_metadata.position`, converted by `Camera2LensPose`).
- `PermissionGate`: one batched request; camera manager enabled whatever the answer and restarted on a later grant; scene managers only with `USE_SCENE`; grants read through the Android API.
- `PinchPushToTalk` (`IVoiceActivation`): hysteresis pinch per hand, gated at pinch start by XRI hover/UI hover and palm-toward-head, dominant/either hand, controller button; `XrUserPoseProvider` with pointing ray, hand key points and a head-pose history.
- `FloorNavMeshBuilder` + `NavMeshBaker`: concave floor outlines triangulated to mesh colliders, bounding boxes baked Not Walkable, Nubi-sized agent settings, rebuilds throttled to 2 s; `ArWorldQuery` (depth raycast + NavGeometry colliders, floor-height filter, furniture labels) and `ArSpatialAnchors` (persistent GUID ids).
- `InteractionSignals`: head-gaze dwell, open palm toward Nubi, XRI poke.
- `QuestBuild` builds Addressables content before the player; setup enables the Meta bounding-box feature.
- Fakes: `FakeFrameSource`, `FakeUserPoseProvider`, `FakeWorldQuery`, `FakeSpatialAnchors`, `FakeVoiceActivation`, `FakeInteractionSignals`.

### WP6

### WP7

### WP8

### WP9

### WP10

### WP11

### WP12
