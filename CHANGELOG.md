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
- `Jolly.Net`: incremental `SseLineParser` (split UTF-8, CRLF across chunks, multi-line data), `HttpTransport` over UnityWebRequest with a pooled 32 KB `SseDownloadHandler` and PlayerLoop-observed cancellation, `StreamTimeout` (first-event and idle windows on `IClock`), `RetryPolicy` (full jitter, Retry-After capped at 8 s) and `RetryExecutor`.
- `Providers/MetaApi/Common`: `MetaApiEndpoint` (bearer auth, missing key fails before any request), `MetaErrors` (status and stream-error mapping), `MetaModelResolver` (throws for a contributor model on the LiveUser route), `MetaSseClient` (retries only before the first event).
- `ResponsesChatModel : IChatModel` on `POST /v1/responses` (`store:false`, encrypted-reasoning replay, flat tools, `UserImage` → `input_image` data URL); every request reserves budget and is settled once with actual usage or an abort estimate.
- `Core/Usage`: `PriceTable` (unknown model → most expensive rates) and persisted `ApiUsageMeter` with reservations, estimates, 2 s debounced atomic saves and `Flush`.
- Recorded Responses SSE fixtures (contributor model, synthetic prompts, ≈ $0.0004) plus hand-written `response.failed`/`error` fixtures; `RecordingUsageMeter` fake.
- Review fixes: SSE decode buffer sized for a carried-over surrogate pair; the meter refuses a second settle and moves an unreadable `usage.json` to `usage.json.bad`; null or mistyped stream fields map to `ProviderException`; abort estimates bill input once per attempt not refused with a status; a completed stream is read to its end instead of aborted.

### WP2
- `ConversationStateMachine`: table-driven, three latched flags, every architecture §6 row plus `SpeechQueued` and `Announce`.
- `ConversationController` + `TurnRouter` + `TurnPipeline` + `BargeInCoordinator`: routed PTT turns, barge-in that cancels before flushing, per-state watchdogs, memory kept after each turn, persona switch applied only when Idle with a spoken greeting, a locale change that ends the running turn, and the persona safety notice given at startup and on a switch (captioned even if TTS fails, canned when offline).
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
- `ConnectivityMonitor`: five states by priority (Unauthorized > BudgetReached > Online/Degraded/Offline). Fed by provider reports, a 1 s reachability poll and health probes with 5 → 30 s backoff. Only a 401 from a Meta provider (`meta`, `meta.*`) marks the key rejected; the probe is off without a key and stays off for a rejected key until the key changes. One probe runs at a time, and a probe result older than a later provider report is dropped.
- `MetaHealthProbe`: `GET /v1/models` with a 3 s timeout on `IClock` that cancels the request, plus an `Unauthorized` event on 401. A 429 counts as reachable. HTTP goes through the `IHttpStatusClient` seam (`UnityWebRequestStatusClient`, which discards the body).
- `OfflineResponder`: plays one canned line per call, with the caption at the first played sample. Offline requests alternate Offline lines with WhoAmI/Greeting fillers. Missing categories fall back to the requested category, then to Error.
- `IdleScheduler`: weighted look-around, stretch, wander (≤ 1.5 m) and glance at the user every 8–20 s, only while the conversation is Idle and Nubi is not moving or following.
- `InteractionTriggers`: gaze dwell and open palm → Greeting, poke → WhoAmI. At most one request per 20 s across all signals, and signals that arrive while Nubi cannot speak don't count toward the limit.

### WP11

### WP12
