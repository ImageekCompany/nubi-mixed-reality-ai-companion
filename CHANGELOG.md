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
- `ConversationStateMachine`: table-driven, three latched flags, every architecture §6 row plus `SpeechQueued` and `Announce`.
- `ConversationController` + `TurnRouter` + `TurnPipeline` + `BargeInCoordinator`: routed PTT turns, barge-in that cancels before flushing, per-state watchdogs, memory kept after each turn, persona switch applied only when Idle with a spoken greeting.
- `TranscriptAliases`, `StopGuard` and `CrisisGuard` with built-in en/pl phrase lists used when the `Crisis` table entry is missing.
- `AgentLoop`: up to 4 rounds, tools started as calls stream in (Parallel / Locomotion supersede / Exclusive), per-tool timeouts, one memory commit (also interrupted and failed turns).
- `ToolRegistry` with a JSON Schema subset validator and the full tool catalogue, including `capture_and_identify`, `find_object` and `highlight_part` against Core ports.
- `ConversationMemory` and `SceneMemory` with hand-mapped DTOs, `AtomicFile`, caps and summarisation; `PersonaRepository`.
- `JsonFileSecretsProvider`, `DotEnvSecretsProvider`, install-scoped safety identifier, `LatencyTracker` (`evt=turn.latency`) and `RedactingLog`.

### WP3

### WP4

### WP5

### WP6

### WP7

### WP8

### WP9

### WP10

### WP11

### WP12
