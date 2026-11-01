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

### WP7

### WP8

### WP9

### WP10
- `ConnectivityMonitor`: five states by priority (Unauthorized > BudgetReached > Online/Degraded/Offline). Fed by provider reports, a 1 s reachability poll and health probes with 5 → 30 s backoff. The probe is off without a key and stays off for a rejected (401) key until the key changes.
- `MetaHealthProbe`: `GET /v1/models` with a 3 s timeout on `IClock` that cancels the request, plus an `Unauthorized` event on 401. HTTP goes through the `IHttpStatusClient` seam (`UnityWebRequestStatusClient`, which discards the body).
- `OfflineResponder`: plays one canned line per call, with the caption at the first played sample. Offline requests alternate Offline lines with WhoAmI/Greeting fillers. Missing categories fall back to the requested category, then to Error.
- `IdleScheduler`: weighted look-around, stretch, wander (≤ 1.5 m) and glance at the user every 8–20 s, only while the conversation is Idle and Nubi is not moving or following.
- `InteractionTriggers`: gaze dwell and open palm → Greeting, poke → WhoAmI. At most one request per 20 s across all signals, and signals that arrive while Nubi cannot speak don't count toward the limit.

### WP11

### WP12
