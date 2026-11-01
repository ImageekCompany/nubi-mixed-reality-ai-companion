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
- `Resampler`: streaming mono rate converter (linear interpolation, 63-tap Blackman-sinc anti-alias when downsampling), split-invariant and allocation-free; also serves WP7's 24 kHz → output-rate path. `Pcm16` float ↔ s16 and RMS.
- `UnityMicrophoneCapture` + `MicrophoneCaptureSession`: 48 kHz device ring → 24 kHz 80 ms pooled `PcmChunk`s, pumped on the PlayerLoop; a 500 ms stall watchdog restarts the mic (device spike §2.6) and ends the stream after three fruitless restarts (a restart is fruitful only once 500 ms of audio follows it); capture times are rebased across a restart gap; `Stop` flushes, `Dispose` aborts.
- `ClientWebSocketConnection` (`Jolly.Net.WebSockets`): main-thread-resuming `ClientWebSocket` wrapper behind `IWebSocketConnection`; messages over 1 MB fail as `Protocol`.
- `MuseRealtimeSpeechToText`: realtime push-to-talk ASR. Connects when `Transcribe` is called, not on first enumeration. Handshake backlog sent at once after the ack, live audio forwarded as it arrives and never more than 5 s ahead, `endStream` in the same continuation as source completion, cumulative partials de-duplicated, exactly one normalised Final, 5 s timeout on `IClock` over connect, handshake and ack, error frames and close codes mapped to `ProviderErrorKind`, usage reserved before connecting and settled with the audio seconds sent.
- `MuseFileSpeechToText`: `POST /v1/asr/transcribe` fallback (buffered WAV, one Final, 10 s timeout). Both adapters share `AsrSessionStream`.
- Recorded ASR fixtures (`Tests/Fixtures/asr`), and shared fakes `FakeAudioCapture`, `ScriptedSpeechToText`, `StreamCollector<T>`.

### WP4
- `OneBitMaskDecoder`: C# port of the SAM 3.1 `one_bit` mask codec (SAM License, `LICENSE-SAM.txt` beside it), bit-exact against the live pen and mug masks.
- `SamOutputParser` (box/mask grammar, inclusive boxes, multi-object, empty output) and `SamPromptFormatter` (concept, point with hand negatives, box; `w`/`h` from the encoded image).
- `SamSegmenter : ISegmenter` streamed through WP1's `MetaSseClient`: returns at `output_text.done` (~150 ms before `response.completed`), usage reservation per image settled as billed unless the server rejected the request with a 4xx, `one_bit` + confidence, masks decoded on the thread pool; an unusable model noun phrase is a no-match.
- `SparkObjectIdentifier : IObjectIdentifier` over `IChatModel`: streamed `LABEL|NOUN_PHRASE` header then speakable answer deltas, per-install `safety_identifier`, 8 s timeout on WP1's `StreamTimeout`, early stop disposes the model stream.
- `ImageDataUrl` moved to MetaApi `Common`, shared by the chat request writer and SAM.
- `JpegImageEncoder : IImageEncoder` (Core/Imaging): thread-pool JPEG encode with the top-row-first → bottom-up flip ImageConversion needs.
- R6 harness `tools/vision-eval/vision_eval.py` and report skeleton `docs/perf/vision-eval.md` (live run pending real room frames).

### WP5
- `CameraMath` (Core/Spatial): pinhole pixel ↔ world ray with a top-left pixel origin and a Y-up Unity camera pose.
- Camera capture: `CameraFrameSlot` (one reused native buffer, one outstanding frame, release from any thread), `EditorStillFrameSource` (fixture image with Quest 3 camera FOV, top row first), `MetaPassthroughFrameSource` (left lens, `ConvertAsync` straight to the target size, head pose interpolated at the image's CLOCK_MONOTONIC timestamp × Camera2 lens pose) and `Camera2Extrinsics` (JNI, by `com.meta.extra_metadata.position`, converted by `Camera2LensPose`).
- `PermissionGate`: one batched request; camera manager enabled whatever the answer and restarted on a later grant; scene managers only with `USE_SCENE`; grants read through the Android API.
- `PinchPushToTalk` (`IVoiceActivation`): hysteresis pinch per hand, gated at pinch start by XRI hover/UI hover and palm-toward-head, dominant/either hand, controller button; `XrUserPoseProvider` with pointing ray, hand key points and a head-pose history.
- `FloorNavMeshBuilder` + `NavMeshBaker`: concave floor outlines triangulated to mesh colliders, bounding boxes baked Not Walkable, Nubi-sized agent settings, rebuilds throttled to 2 s; `ArWorldQuery` (depth raycast + NavGeometry colliders, floor-height filter, furniture labels) and `ArSpatialAnchors` (persistent GUID ids).
- `InteractionSignals`: head-gaze dwell, open palm toward Nubi, XRI poke.
- `QuestBuild` builds Addressables content before the player; setup enables the Meta bounding-box feature.
- Fakes: `FakeFrameSource`, `FakeUserPoseProvider`, `FakeWorldQuery`, `FakeSpatialAnchors`, `FakeVoiceActivation`, `FakeInteractionSignals`.
- Review fixes: Camera2 lens pose no longer disposes the Unity-owned activity (it always fell back on device) and is read on first capture; a held pinch survives tracking dropouts up to 200 ms; race-free `CameraFrameSlot` dispose; camera manager enabled even if the permission request throws; cancellation-safe anchors; planes that lose the Floor label leave the NavMesh; controller-aim pointing fallback and a head-pose prediction lead for R1.

### WP6
- Blender pipeline: byte-identical `Nubi.fbx` rebuilds with no absolute build paths; `Shrug` and `Stretch` clips for the remaining gesture ids; face-preset JSON in a JsonUtility shape with the looping-clip list.
- `NubiModelPostprocessor`: Generic rig, baked axis conversion, bone objects kept, Loop Time from the Blender build.
- `NubiCharacterController : ICharacterController`: NavMesh moves (arrive / unreachable / superseded / timed out; the `WhenArrived` token never stops a walk), follow band, body-first look-at with eyes within ±16°/±8°, gesture clips plus a scripted LookAround, mood and clip face presets, smoothed lip-sync, random and double blinks.
- Generated `Nubi.prefab`, `Nubi.controller` (Locomotion blend tree with playback rate clamped to 0.7–1.5×, upper-body Conversation and Gesture layers), rim/fresnel `NubiSkin` shader and an analytic blob contact shadow that darkens passthrough.
- `ULipSyncDriver : ILipSyncDriver` with silence detection.
- PlayMode tests for locomotion, gestures, Animator parameters, face (tone-driven mouth), import settings and shader compilation.
- Review fixes: look-at restarts from the animated pose each frame (no spin while culled offscreen); full pitch reach with the eyes clamped; a superseded move's awaiter can issue the next command; catch-up uses floor distance; gestures cancel on destroy; overlapping LookAround sweeps.

### WP7
- `WitTextToSpeech`: raw Wit `/synthesize` (`audio/raw`, 24 kHz s16le) over `UnityWebRequest` with a pooled 32 KB `DownloadHandlerScript` buffer; `WitPcmChunker` carries odd bytes across HTTP chunks; 3 s first-chunk and 3 s inter-chunk timeouts on `IClock` (`PcmStreamTimeout`), surfaced as `ProviderException(Timeout)`; HTTP status mapping; English only (`Supports`).
- `StreamingSpeechOutput`: clip-less `AudioSource` fed from `OnAudioFilterRead`. `SpeechRingPlayer` (audio thread: SPSC ring, exact integer-ratio linear resampling 24 kHz → output rate, 20 ms flush fade, block RMS) + `SpeechOutput` (main thread: per-call prefetch, strict call-order writes, `FirstSamplePlayed` and completion from the consumed position, `Flush` cancels every pending `Play`/`PlayClip`); the source restarts after an audio device change or re-enable.
- `AmplitudeLipSyncDriver`: RMS → mouth "A", instant attack, closed within 100 ms of silence.
- `SentenceSplitter` (terminators + closers, abbreviations/initials, decimals, newline, 60-char first chunk on a comma, 120-char comma/space split, 240 cap, `。`) and `SpeechQueue` (prefetch depth 1, user speech rate clamp 50–200, `Clear` on barge-in, `WhenDrained` never hangs after a flush, `SpokenSoFar`, `SentenceStarted`/`SentenceFailed` in play order, so sentences may share an utterance id; `Enqueue` rejects text over the TTS limit).
- `CannedLines : ICannedLines` (no repeat of the category's last 5 picks, localized text with English fallback, clip via the `ILineClipSource` port) and `CannedLineCatalog` (31 English lines over every `LineCategory`).
- `CannedLineBaker` (editor menu + batch method) bakes the catalog through the runtime Wit client; 31 English lines baked to `Content/Audio/Lines/en` with Wit voice `Charlie` (Decompress On Load, ADPCM, mono, not normalized so offline loudness matches live Wit).

### WP8
- Unity Localization set up: settings asset, locales `en` (default) and `pl`, the `UI` string table (en + pl), Addressables with `BuildWithPlayer`, and `LocaleVoiceMap.asset` (Polish: ASR bias only, no Wit voice).
- `UnityLocalizer`: preloads tables at startup and on locale change, synchronous `Get` with key fallback and a warning, `evt=loc.ready` per table, static-event unsubscribe on `Dispose`.
- Views in `Jolly.Presentation`: `CaptionView`, `MaskOverlayView` (`IMaskPresenter`, `Jolly/MaskOutline` fill + outline shader), `ToolBadgeView`, `LatencyHud`, `StatusIndicator` (`IStatusSink`) and `SettingsPanel` (`IUserPreferences`; caption size, speech rate, volume, seated, PTT hand, persona, language, clear memory, API usage), shipped as world-space prefabs.
- TMP Essential Resources imported (needed by every TMP text).
- 51 PlayMode tests under `Tests/PlayMode/Presentation` (localizer, caption timing, overlay placement, badges, HUD, status, settings); contract fakes shared in `Tests/Shared/Fakes/WP8`.
- Review fixes: a cancelled `SetLanguage` no longer leaves the language half-switched; Polish falls back to English per entry; running tool badges end when the turn ends; the mask outline sets its texel size explicitly; stored preferences are clamped on load.

### WP9
- `JollyBootstrap` + `AppGraph`: the composition root wires every WP1–WP10 implementation in dependency order with one disposable stack, flushes usage and memory on pause/focus loss and places Nubi on the floor in front of the user. Provider outcomes reach `ConnectivityMonitor` through decorators; the probe's 401 marks the key rejected. Two Responses chat models (voice 6 s × 2, background 10 s × 3); every model id through `MetaModelResolver`.
- `Edges`: files, secrets, network, HTTP/WebSocket, Wit, microphone, push-to-talk, speaker and camera are swappable at the provider edge only.
- Personas (Companion default, Mentor, Guru, Psychologist) with en/pl prompts in a new `Personas` table; a `Lines` table (en, pl); `JollyConfig.asset`; `LineClipLibrary` with the 31 baked clips (ADR-0018).
- `Main.unity` fully wired: AR managers ship disabled behind `PermissionGate`, `ARAnchorManager`, a poke-only `NubiPoke` interaction layer on Nubi, `StreamingSpeechOutput` + uLipSync on Nubi's voice object, all views, an `XRUIInputModule` EventSystem.
- Content validators (`Jolly/Validate Content`), now a `tools/ci/test.sh` gate.
- E2E PlayMode tests on the real scene with doubles only at the provider edges: scripted voice turn, barge-in ≤ 150 ms, capture_and_identify on the still frame, offline press, `NoGcAllocInSteadyStateIdle`; `ULipSyncDriver` tests.
- Device self-test (`Runtime/App/SelfTest`, `tools/device/selftest.sh`): real ASR → Spark → Wit → output on the Quest from bundled clips, offline lines, vision with a dark-room fallback, barge-in silence and a 30 s perf sample; results in `docs/perf/selftest-report.md`.
- Fixes found by the E2E test and the device run: the tool preamble is spoken when a tool starts (no more glued sentences), the health probe URL (`/v1/v1/models`), SAM billing per possibly-billed attempt; captions-only replies are paced by reading time.
- One `PixelToCameraDir` (Contracts), one HTTP status mapping and one HTTP path for the probe.

### WP10
- `ConnectivityMonitor`: five states by priority (Unauthorized > BudgetReached > Online/Degraded/Offline). Fed by provider reports, a 1 s reachability poll and health probes with 5 → 30 s backoff. Only a 401 from a Meta provider (`meta`, `meta.*`) marks the key rejected; the probe is off without a key and stays off for a rejected key until the key changes. One probe runs at a time, and a probe result older than a later provider report is dropped.
- `MetaHealthProbe`: `GET /v1/models` with a 3 s timeout on `IClock` that cancels the request, plus an `Unauthorized` event on 401. A 429 counts as reachable. HTTP goes through the `IHttpStatusClient` seam.
- `OfflineResponder`: plays one canned line per call, with the caption at the first played sample. Offline requests alternate Offline lines with WhoAmI/Greeting fillers. Missing categories fall back to the requested category, then to Error.
- `IdleScheduler`: weighted look-around, stretch, wander (≤ 1.5 m) and glance at the user every 8–20 s, only while the conversation is Idle and Nubi is not moving or following.
- `InteractionTriggers`: gaze dwell and open palm → Greeting, poke → WhoAmI. At most one request per 20 s across all signals, and signals that arrive while Nubi cannot speak don't count toward the limit.

### WP11
- `README.md` (pitch, theme fit, Meta tooling, architecture, performance budget, privacy, getting started, status, roadmap), MIT `LICENSE`, and `THIRD_PARTY.md` with the versions resolved in `packages-lock.json`, non-OSI licences, committed third-party files and why Meta's Jolly mascot is not included.
- `docs/demo-script.md` (six-beat shot list, per-beat fallbacks, Nov 9 live-finals runbook, rehearsal log) and `docs/perf/README.md` (OVR Metrics, Unity Profiler and `evt=turn.latency` percentile method).

### WP12

### Integration review fixes
- A press while a gaze/palm/poke line plays now silences it; Clear memory waits for Idle and deletes every persona's file; a missing microphone plays the Error line, not the offline one.
- The crisis hand-off runs for every persona; a tool call outside the offered list is refused; Psychologist is shown as Listener / Słuchacz and may switch persona by voice; persona prompts cover every tool and no longer assume an older user; tool preambles stay neutral.
- An empty vision answer speaks the hold-closer line instead of ending the turn silently; the ASR stream reports success at its first item; "Nobi" is aliased to "Nubi".
- Self-test: adb-only trigger (no intent extra), render counters and a 71.5 fps floor in `perf_idle`, Wit-synthesised voice clips, compositor stale-frame share in `selftest.sh`, install retry. Release players no longer capture stack traces for Info logs, and canned clips no longer warn.
- README, demo script, THIRD_PARTY and the self-test report updated from three device runs (release → first audio p50 1.57 s over 9 turns, barge-in silence 28–54 ms, 72.0 fps idle, 12 draw calls).

### Performance and latency (fix/perf-latency)
- Self-test frame diagnostics: `evt=frame.spike` for every main-thread frame over 28 ms, a per-phase `evt=selftest.frames` window (GC bytes per frame in development players), `evt=vision.timing` (header, answer text and mask from the tool start) and the `tool_to_answer_audio_ms` metric.
- Pipelines warmed at boot from a `GraphicsStateCollection` recorded by a development-player self-test (`GraphicsWarmup`, `GraphicsStateImport`, docs/perf/README.md §7).
- Launch warm-up: one discarded camera capture (Camera2 lens pose, CPU-image conversion, JPEG encoder) and one ASR connection without a handshake (not billed).
- Every Responses request carries a `prompt_cache_key` (hash of model, instructions and tool names): tool-bearing requests were never cached without one.
- Vision output cap 600 → 1,024 tokens (a device call ended `finish=Length`); summary output cap 300 → 1,024, and a summary call that ends without text no longer replaces the summary.

### LLM memory and prompt caching (ADR-0020)
- Summaries finish again: `max_output_tokens` 300 → 1,024 (it includes reasoning, which took 188 of the 300), and the prompt asks for terse notes of at most 150 words. A reply that is cut off or empty is rejected and the old summary is kept. Before this, a cut-off reply was stored and an empty one erased the memory.
- Cache-stable request prefix: the per-turn context block now comes after the recent items, the body writes `prompt_cache_key`, `instructions` and `tools` before `input`, and a test checks that the next turn's body starts with the previous one up to its context block.
- Persona prompts are shorter: the tool paragraph repeated in six strings is gone, tool "when to call" guidance is in the tool descriptions, and the shared rules cover spoken length, stage directions, claims about seeing things, tool failures and never asking for passwords or bank details. The Mentor persona now says to unplug devices before cleaning, and the Listener persona no longer mentions camera tools.
