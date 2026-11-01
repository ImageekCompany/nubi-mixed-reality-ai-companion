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

### WP6
- Blender pipeline: byte-identical `Nubi.fbx` rebuilds with no absolute build paths; `Shrug` and `Stretch` clips for the remaining gesture ids; face-preset JSON in a JsonUtility shape with the looping-clip list.
- `NubiModelPostprocessor`: Generic rig, baked axis conversion, bone objects kept, Loop Time from the Blender build.
- `NubiCharacterController : ICharacterController`: NavMesh moves (arrive / unreachable / superseded / timed out; the `WhenArrived` token never stops a walk), follow band, body-first look-at with eyes within ±16°/±8°, gesture clips plus a scripted LookAround, mood and clip face presets, smoothed lip-sync, random and double blinks.
- Generated `Nubi.prefab`, `Nubi.controller` (Locomotion blend tree with playback rate clamped to 0.7–1.5×, upper-body Conversation and Gesture layers), rim/fresnel `NubiSkin` shader and an analytic blob contact shadow that darkens passthrough.
- `ULipSyncDriver : ILipSyncDriver` with silence detection.
- PlayMode tests for locomotion, gestures, Animator parameters, face (tone-driven mouth), import settings and shader compilation.
- Review fixes: look-at restarts from the animated pose each frame (no spin while culled offscreen); full pitch reach with the eyes clamped; a superseded move's awaiter can issue the next command; catch-up uses floor distance; gestures cancel on destroy; overlapping LookAround sweeps.

### WP7

### WP8

### WP9

### WP10

### WP11

### WP12
