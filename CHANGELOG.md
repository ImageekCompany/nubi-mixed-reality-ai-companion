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
- The vision request tells the model it has no tools: persona prompts ask for `capture_and_identify`, and on the headset one vision reply spent 40 s reasoning aloud about the missing tool.
- Vision output cap 600 → 1,024 tokens (a device call ended `finish=Length`); summary output cap 300 → 1,024, and a summary call that ends without text no longer replaces the summary.

### LLM memory and prompt caching (ADR-0020)
- Summaries finish again: `max_output_tokens` 300 → 1,024 (it includes reasoning, which took 188 of the 300), and the prompt asks for terse notes of at most 150 words. A reply that is cut off or empty is rejected and the old summary is kept. Before this, a cut-off reply was stored and an empty one erased the memory.
- Cache-stable request prefix: the per-turn context block now comes after the recent items, the body writes `prompt_cache_key`, `instructions` and `tools` before `input`, and a test checks that the next turn's body starts with the previous one up to its context block.
- Persona prompts are shorter: the tool paragraph repeated in six strings is gone, tool "when to call" guidance is in the tool descriptions, and the shared rules cover spoken length, stage directions, claims about seeing things, tool failures and never asking for passwords or bank details. The Mentor persona now says to unplug devices before cleaning, and the Listener persona no longer mentions camera tools.

### Controller push-to-talk
- `ControllerPushToTalk` (`IVoiceActivation`): hold A or X on a Touch controller to talk. Two code-defined Input System actions bound to the Touch layouts by name (`<OculusTouchController|QuestTouchPlusController|QuestProTouchController>{LeftHand|RightHand}/primaryButton`), so neither XRI's `Activate` nor any hand device (Hand Interaction profile, a pinch reported as a trigger, the KHR simple controller's `PrimaryButton` select) can press past the pinch gating. A side whose hand is tracked is in hand mode and its button is ignored, as in `XRInputModalityManager` (ADR-0021).
- `CompositeVoiceActivation` (Core): one push-to-talk over the pinch and the controller, held while either is held; a second source neither re-presses nor releases early. `AppGraph` builds it when `Edges.Voice` is null.
- `PinchPushToTalk` is hands only: the empty `controllerTalk` action reference is gone from the component and `Main.unity`, which gains `SceneRefs.controllerPushToTalk`.
- Tests: `InputTestFixture` PlayMode tests on the real OpenXR layouts (A/X, trigger/grip/B/Y, hand devices, tracked-hand gate, disable) and EditMode tests for the composite. `Jolly.Tests.PlayMode` references `Unity.InputSystem.TestFramework` and `Unity.XR.OpenXR`.
- README: Controls section; the "no controller push-to-talk" limitation is replaced by "unverified on the headset".

### Audit fixes (ADR-0022)
- Clear memory, a replaced place and an evicted place or object now erase their spatial anchors on the headset (`ISpatialAnchors.Erase`, `ARAnchorManager.TryEraseAnchorAsync`).
- The device self-test keeps its memory in `selftest/memory/`, emptied per run, instead of reading and writing the user's.
- `Jolly.Spikes` (camera-frame and mic dumps) is compiled only with `JOLLY_SPIKES`, so release APKs no longer contain it.
- `RedactingLog` also redacts exception text; place names and the summary can no longer close the prompt's `<context>` block.
- A failed usage save is logged (`evt=usage.save_failed`) and retried instead of throwing out of `OnDestroy`, which skipped the app's teardown. An interaction line cut short by teardown no longer touches the destroyed character.
- `push-secrets.sh` force-stops the app (secrets are read at launch) and warns when `.env` is readable by other accounts.
- Removed: the never-wired file-ASR fallback (`MuseFileSpeechToText`, `UnityWebRequestAsrFileClient`, `MuseTranscribeRequest`; spike R4 made it unnecessary), the unused `MetaApiEndpoint.Get`, the unused `Unity.AI.Navigation` reference of `Jolly.XR`, and the renders of Meta's Jolly mascot from `docs/research/samples/`.
- `ClipPcm` resamples an off-rate clip with the anti-aliased `Resampler` instead of its own linear interpolation.
- Tests: duplicated fakes merged into `Fakes/Common` (one per port, no aliases); ASR tests use `RecordingUsageMeter`, which rejects a double settle like the real meter; locomotion tests bake with the production `NavMeshBaker`.

### Turn GC and the cold first turn (ADR-0023)
- `selftest.sh --profile` + `TurnProfileCapture`: development players record each turn scenario to a Profiler log with allocation call stacks; `Jolly.Editor.Tools.GcAllocReport` attributes them in batchmode.
- `ClientWebSocketConnection` opens socket, TLS and the RFC 6455 upgrade itself (`WebSocketUpgrade`) and wraps the stream with `WebSocket.CreateFromStream`: Mono's `ClientWebSocket` read the 101 head one byte per TLS read at 16 KB of garbage each.
- ASR audio is sent in frames of at least 240 ms (`MuseAsrOptions.MinFrameSeconds`); the remainder goes out as soon as the capture ends.
- `SseLineParser` reuses `char[]` buffers and event names, `HttpTransport` reuses its SSE sinks, and `ResponsesStreamReducer` reads only id, usage, incomplete_details and error from the response envelope events.
- `PcmChunk` and `ClipPcm` use dedicated array pools; the self-test converts its clips at launch.
- Launch warm-up opens the Wit TTS connection (`GET /voices`, nothing synthesized).
- Development player, 3 runs each: turn GC p50 14.5 → 3.9 KB/frame, max 38.0 → 6.4 (`barge_in`); first turn release → first audio 2.2–3.8 s → 1.7–1.8 s.
- EditMode tests: `WebSocketUpgradeTests`, `SseLineParserAllocationTests`, `ResponsesStreamReducerAllocationTests`, packed ASR frames.
### Mixed-reality occlusion and contact shadow (ADR-0024)
- Real objects and the user's hands hide Nubi and its shadow. Primary: Quest environment depth through AR Foundation shader occlusion: the "Meta Quest: Occlusion" OpenXR feature (hand removal off), `AROcclusionManager` + `ARShaderOcclusion` (soft) on the Main Camera, both shipped disabled; `PermissionGate` starts the manager with `USE_SCENE`.
- `Content/Rendering/Shaders/EnvironmentOcclusion.hlsl`: hard and soft depth tests against `_EnvironmentDepthTexture(Preprocessed)` with a 4 cm bias, respecting `_IsOcclusionOn`. `NubiSkin` writes premultiplied colour with alpha = visibility and `BlobShadow` scales its alpha, so hidden pixels show passthrough; no extra draws.
- Fallback (scope Should 3): `SceneOccluders` draws a cube (`Jolly/XR/DepthOccluder`, `M_SceneOccluder`) per Space Setup bounding box after Nubi and before the UI, writing the clear colour and no depth where it is nearer, so world-space UI is never hidden by it. Needs `USE_SCENE` like the bounding boxes: denied, nothing occludes.
- `RealWorldOcclusion` + pure `OcclusionPolicy`: depth while a depth frame arrived in the last 0.5 s, else scene meshes, else nothing; logs `evt=occlusion.mode`. Tunable in `JollyConfig.occlusion` (preference Off/SceneMeshes/EnvironmentDepth, soft edges, fallback, stale window). `SceneRefs.occlusion` wired in `Main.unity` and `XrServices`.
- Contact shadow: `ContactShadow` (part of `NubiCharacterController`) fades the blob to 0 and shrinks it to 60 % as the lower foot rises to 0.3 m above its rest height; `NubiPrefabBuilder` binds the quad, `Foot_L`/`Foot_R` and the bind-pose rest height (0.06 m) into `Nubi.prefab`.
- Tests (PlayMode): `OcclusionPolicyTests`, `ContactShadowMathTests`, `SceneOccludersTests`, `RealWorldOcclusionTests`, `ContactShadowTests` on the real prefab, and `MixedRealityWiringTests` on the booted `Main.unity`.
- Docs: ADR-0024, architecture §2/§5.5/§11, scope Should 3, README limitations, release checklist human action 10 (device checks).

### Release audit
- `docs/release-checklist.md` walks the definition of done on `main` with evidence and lists the human actions left. README test counts, status line, spend figure and Device User Data note corrected; the DoD secret grep excludes the synthetic keys in `Tests/`, and its perf deviations point at `docs/perf/selftest-report.md`.

### Coverage gate
- `com.unity.testtools.codecoverage` 1.3.0 (bundled with 6000.6.6f1). `tools/ci/test.sh` runs EditMode and PlayMode with coverage over the `Jolly.*` runtime assemblies and writes the merged report to `artifacts/coverage/Report`; `tools/ci/coverage.sh` prints line coverage per assembly and fails when Core + Net + Providers.MetaApi + Providers.Wit + Audio together fall below 91.6 % (measured 93.6 % minus 2 points; 92.7 % before the new tests).
- 23 EditMode tests on the lowest-covered logic: `MemoryKeeper` failure and cancellation paths, a turn that throws a non-provider exception, a refused or crashing persona switch, SAM `response.incomplete` / `error` / malformed events, `MetaErrors` edge mappings, the health probe's `TransportStatusClient`, and `find_object`'s instance cap. EditMode 904 → 927.

### Hand-tracked rig (ADR-0030)
- Fixed: with hands, nothing was drawn and nothing could be pressed. The Starter Assets rig's `XRInputModalityManager` had no hand objects, so a tracked hand deactivated the controller objects and nothing replaced them; the pinch gate read those inactive controller rays, so pinching a button also started a turn.
- `Main.unity` uses the XRI 3.6.1 *Hands Interaction Demo* rig variant `XR Origin Hands (XR Rig)` (switched with `ReplacePrefabAssetOfPrefabInstance`; all overrides and references kept): Quest hand meshes (`Unity_Hand_Medium`, hidden while untracked), hand Near-Far rays and pokes for the Settings UI and Nubi, the palm-up system-gesture guard, and controller ↔ hand switching per side.
- XRI *Hands Interaction Demo* sample committed at the Package Manager path (without its demo scene, `DemoAssets` and `InteractionAffordance.prefab`, which need the Video module). Its dependency, the XR Hands 1.9.0 *HandVisualizer* sample, is under the Unity Package Distribution License, so it is ignored by git and restored from the package cache with its GUIDs: `tools/ci/restore-samples.sh` runs first in `tools/ci/test.sh`, `tools/ci/build.sh` and `tools/device/build.sh`, and the `[InitializeOnLoad]` `PackageSampleRestore` covers a clone opened in the Editor.
- `PinchPushToTalk` gates on the hand Near-Far interactors; the hand pokes reach Nubi's `NubiPoke` layer like the controller pokes.
- `HandTrackingMonitor` logs `evt=hands.subsystem`, `evt=hands.tracked` and `evt=hands.visual` on change (`SceneRefs.handMonitor`, built in `XrServices`).
- `SceneRules` fails content validation when the modality manager has no hand objects or the pinch rays are not under them. OpenXR features unchanged: every hand feature the reference project uses was already enabled, and the manifest already carries `com.oculus.permission.HAND_TRACKING` and `oculus.software.handtracking`.
- Tests: `HandTrackingMonitorTests` (PlayMode).
### Room awareness (ADR-0025)
- Unscanned rooms: with the scene permission granted and no floor plane 8 s later, the room counts as not scanned. Nubi then walks on a 1.5 m fallback disc around the user at the XR Origin floor height, stretched to cover Nubi too so it is never stranded when the user walks off (past 4 m Nubi pops back beside the user) (logged `evt=room.degraded walkable=fallback`), offers Space Setup once per session with the new canned line `room_scan_01`, and opens it through `MetaOpenXRSessionSubsystem.TryRequestSceneCapture` when the model calls the new `request_room_scan` tool. Planes and boxes are re-queried when the app resumes.
- Real furniture: new `go_to_surface` tool (couch, table, desk, bed, chair, storage, screen) stops Nubi beside the nearest one, facing it; `walk_to` approaches furniture the same way and also takes seen-object labels ("mug"); `look_at` takes object labels and furniture types.
- The prompt's context block has a room line: scanned furniture with distances, or that the room is not scanned. One static instruction sentence mentions it.
- Contracts: `IWorldQuery.WalkableArea` and `GetSurfaces` replace `HasWalkableArea` and `TryFindLabeledSurface`; new `SceneSurface`, `IRoomScan`, `ISceneData`, `ICharacterController.Relocate`, `LineCategory.RoomScanOffer`. `SceneRefs.session` binds the AR Session. `JollyConfig` gains `roomScanTimeoutSeconds`, `fallbackFloor`, `fallbackFloorRadius` and `offerRoomScan`.
- Self-test `voice_come_here` passes on the fallback floor and reports `navmesh=fallback` and `walk=Arrived`; it is degraded only with no walkable area at all.
### Swappable characters (ADR-0026)
- `CharacterDefinition` asset per character: model, Animator controller, clip map, face presets, canonical face channel → blendshapes/jaw mapping, look-at and upper-body bone names, body sizes (scale, height, agent and collider radius, contact-shadow radius) and tuning. Nubi's values moved from the prefab and `NubiPrefabBuilder` into `Content/Character/Nubi/NubiCharacter.asset`; `JollyConfig.defaultCharacter` / `characters` list it.
- `NubiCharacterController` is data-driven: `TryUse(definition)` swaps another model into the Nubi shell before `Construct` (bound by name through `CharacterRig`, nothing changes on failure); `FaceRenderer` maps canonical channels to any blendshape names or a jaw bone; `CharacterBody` sizes agent, collider and shadow.
- Selection at launch (`CharacterSelection`): `persistentDataPath/character.txt` (adb, or Jolly/Character/Play As Selected Definition), then `tools/device/build.sh --character <id>`, then the default; unknown, unusable or unbindable choices log a warning and fall back to Nubi.
- Local demo characters live in the git-ignored `Assets/Jolly/Local/` (plus `docs/art/local/`). `CharacterSceneProcessor` adds them to the processed scene only: all in Play mode, in a player build only the one `--character` names.
- `CharacterModelPostprocessor` (was `NubiModelPostprocessor`) imports any FBX with a sibling `_FacePresets.json`; `CharacterAnimatorBuilder` (was `NubiAnimatorBuilder`) builds any definition's controller (Jolly/Character/Build Animator For Selected Definition).
- `CharacterDefinitionValidator` (Jolly/Character/Validate Characters, and the content validator in `tools/ci/test.sh`) checks the rig contract on every definition in the project. PlayMode: `CharacterDefinitionTests` (skips the local Jolly definition when absent), `CharacterSwapTests`, `FaceRendererTests`, `CharacterSelectionTests`. How to add a character: `docs/characters.md`.
- With occlusion (ADR-0024): the contact shadow is sized by the definition's `contactShadowRadius` and follows the feet named in `bones.footLeft`/`footRight`; their rest height is read from the model asset's bind pose, so a swapped character needs no extra data. The controller binds `ContactShadow` at `Construct`; a character without feet keeps a standing shadow (validator warning).
- The character says its own name: string tables and `CannedLineCatalog` use `{name}`, filled by `CharacterNamedLocalizer` from the selected definition's `displayName` (or `nameKey`), for persona prompts, greetings, canned lines and stop fillers. `CharacterDefinition` gains `aliases` (ASR mishearings, replacing the hard-coded `TranscriptAliases.Default`) and `lineClips` (named lines in its own voice; `CharacterLineClips` never plays a shared clip for them). Nubi's `greeting_01` and `whoami_01..03` moved to `Content/Character/Nubi/Lines/en/`. Tool descriptions and the memory summariser no longer say "Nubi".
- `CannedLineBaker`: the shared bake skips named lines; **Jolly ▸ Character ▸ Bake Named Lines For Selected Definition** (`BakeCharacterBatch -jollyCharacter <asset>`) bakes them per character. `ConfigRules` requires the default character's named clips and none in the shared library.
- `CharacterSelection.Bind`: an override that resolves but fails to bind falls through to the baked `--character` id before the default; a definition that failed is not retried. `CharacterSceneProcessor` logs the local definition paths a build includes.
- `tools/blender/build_jolly.py` writes to `Assets/Jolly/Local/Characters/Jolly/`, where the swap finds local characters; the pre-swap `Jolly.prefab` and override controller are gone (docs/art/jolly-demo.md).

### Device self-test findings (2026-11-01)
- Late Responses first event: a model round with no first event after 2 s raises `AgentEventKind.Delayed` and Nubi says a canned Acknowledge line ("One moment."), once more 4 s later while the retry runs; if the retry fails too the Error line follows. The voice first-event timeout drops from 6 s to 4 s (2 attempts, ≤ 8.5 s instead of 12.5 s of waiting; request → first event was 0.3–1.4 s in 52 device turns), which also gives the vision retry room inside its 8 s. `JollyConfig.voiceFirstEventSeconds`, `holdLineSeconds`, `holdLineRepeatSeconds`.
- Self-test verdicts (`TurnChecks`): a failed key tool fails its scenario (it counted as completed, so a vision turn whose `capture_and_identify` failed passed); release → first audio over 2.0 s and tool start → answer audio over 2.5 s degrade with the reason (they were notes on a PASS); no answer after the tool fails; another tool that failed degrades.
- Wit warm-up: `StartupWarmup` synthesizes "Hi." in the persona's voice through the normal streaming `/synthesize` request and discards it, instead of `GET /voices`, which opened the connection but left the synthesis path cold. Quest release runs, first sentence queued → first audio on the first turn: 417 ms before, 417 / 140 / 180 / 222 / 402 ms after (p50 222 ms, was 417–543 ms in four runs).
### Cartoon voice (ADR-0031)
- Nubi speaks with Wit `Rubie`, pitch 110, speed 100, default style, no effect: the winner of a 14-voice audition re-checked on latency, pitch, noise and transcription errors (`docs/research/voice.md` §11). Brighter and more cartoon-like than `Charlie`, still clean on long and sensitive lines.
- `VoiceProfile` asset (`Jolly.Character`, `Content/Character/Nubi/NubiVoice.asset`) holds a character's voice (voice, style, speed, pitch, SSML sfx character and environment) with per-language replacements; `JollyConfig.characterVoice` references it until `CharacterDefinition` owns it. New contract `ICharacterVoice`; `VoiceSpec` gains `SfxCharacter`, `SfxEnvironment` and `Copy()`.
- Personas no longer name a Wit voice: `voiceStyle`, `voiceSpeedPercent` and `voicePitchPercent` scale the character's voice (`VoiceDelivery`). `LocaleVoiceMap` and `LocaleVoiceProfile` lose their TTS voice fields.
- `WitProtocol.SynthesizeBody` wraps XML-escaped text in `<speak><sfx …>` only when an effect is set and never sends `gain` (Wit ignores it, checked live).
- The 31 English canned lines are re-baked in the new voice; `CannedLineBaker` reads the character's voice and warms the Wit connection before the first line.
- `VoiceLipSyncTests` runs the baked lines through the scene's uLipSync profile: every vowel stays reachable with the new voice (I 3.8 % → 10.7 % of voiced frames), so the profile was not retuned.
- Tests: Wit request body per parameter and omission, `VoiceDelivery`, `VoiceProfile` resolution and the shipped Nubi voice, persona voices through `PersonaRepository`.
- `CharacterDefinition.voice` owns the `VoiceProfile`; `JollyConfig.characterVoice` is gone. The selected character's voice reaches `PersonaRepository` (persona voices, the launch Wit warm-up, live speech). A definition without a voice is unusable (`reason="no voice"`, selection falls back to the default) and fails `CharacterDefinitionValidator`, as does one without an English Wit voice. `CannedLineBaker` bakes the shared library in the default character's voice and a character's lines in its own, every line when its voice differs. Tests: `CharacterDefinitionTests` (no voice, no English voice), `CharacterSelectionTests` (no voice falls back), `VoiceProfileTests` (Nubi's definition speaks Rubie).
- Merged after swappable characters (ADR-0026): Nubi's named lines (`greeting_01`, `whoami_01..03`) keep their per-character home in `Content/Character/Nubi/Lines/en/` in the new voice; `room_scan_01`, added since, is re-baked too, so every shared line is Rubie. `CannedLineBaker` bakes shared and named lines in the character voice and warms Wit with it; the launch Wit warm-up already speaks the persona's (now the character's) voice.

### Device-regression fixes (ADR-0032)
- A greeting answers in words: the static rules say to move only when asked, `come_here` and `follow_user` descriptions need the user's request, the companion prompt (en, pl) offers movement "when the person asks". `GreetingScenario` degrades a greeting that starts a movement tool (`TurnChecks.StayedPut`). Device: greetings that walked 4 of 7 → 0 of 7.
- `ModelEventKind.TextDone` (Responses message item done) → `AgentEventKind.SpeechBreak` → the pipeline flushes the splitter, so a preamble without trailing whitespace is spoken before the tool call's arguments stream. Device: first text → sentence queued p50 112 → 0 ms; release → first audio p50 2,090 → 1,776 ms (n=12).
- Launch: `PermissionGate` starts the camera manager a frame after the app graph and each scene manager on its own frame; `QuestVulkan.graphicsstate` re-recorded with the hand rig and depth occlusion (15 variants / 14 states). Worst launch frame 410 → ~283 ms; the rest is the Meta camera provider's start.
- Tests: `MovementTools_DescriptionsSayTheyNeedTheUsersRequest`, `Instructions_MoveOnlyWhenAskedAndAnswerGreetingsInWords`, `StayedPut_*`, `MessageDone_EndsTheTextBeforeTheCallArgumentsStream`, `TextDone_IsASpeechBreakBeforeTheCallStarts`, `SpeechBreak_QueuesTheUnterminatedPreambleBeforeTheToolArrives`, `Run_SceneGranted_StartsTheCameraAndEachSceneManagerOnItsOwnFrame`.

### Self-test SAM text tools and hand debounce (ADR-0033)
- Device self-test scenarios `vision_highlight_part` ("Which part of the mug should I hold?": `highlight_part` must outline a part on the identify turn's own capture) and `vision_find_object` ("Nubi, where is my mug? Show me.": another tool degrades with its name, `TurnChecks.PreferredTool`), both on the bundled still. `MaskObserver` (through `Edges.WrapMasks`) and `TurnObserver` add `tool_to_mask_ms`, `tool_to_answer_audio_ms` and `found`.
- Hand meshes show only after 200 ms of steady tracking: `HandTrackingMonitor` drives each hand renderer from `TrackingDebounce` instead of `XRHandMeshController`'s own show/hide; `evt=hands.tracked` logs the debounced state (38–56 flips per headless run → 3).

### Latency turns and stale frames (ADR-0034)
- `selftest.sh --turns N` pushes `{"turns":N}`; the app runs `LatencyTurnsScenario` (greeting, come here, vision, repeated) and `perf_idle` and reports n / p50 / p90 / max for release → first audio and each stage (`LatencySeries`). The flag is `adb push`ed; an unreadable flag runs the default list. Device: 20 turns p50 1,804 / p90 2,577 / max 2,737 ms (`751acba`).
- Stale frames follow headset uptime, not the build: reboot before a demo (demo script, release checklist). Starting the camera manager on the scene's first frame was measured and reverted.

### Tool descriptions and the tool-selection eval (ADR-0035)
- `come_here` and `find_object` descriptions lead with when to call them; the tool rules say to call the movement tool in the turn the user asks; `look_at` sends object searches to `find_object`.
- `[Explicit]` PlayMode `LiveEval` sends 20 text turns with the app's prompt and tool catalogue and writes the first-round tool calls as a table (`tools/eval/tool-selection.sh`; `tools/ci/test.sh` never runs it).

### Local come-here guard (ADR-0036)
- `ComeGuard`: a final of at most 6 words made only of come triggers and fillers ("Hi Nubi, come here.") runs the registered `come_here` before the model call, when the persona is offered it. `StopGuard` and `ComeGuard` share `ShortCommand`; triggers and fillers are `Crisis` table entries with en/pl fallbacks.
- Self-test: `evt=llm.reply` per model round (tools, finish reason, first 120 characters; self-test only), `voice_come_here` says "Hi Nubi, come here." and reports `come_source` (guard/model).

### Guard walk without a model round; `find_object` gaze (ADR-0037)
- A guard walk ends the turn locally: `TurnPipeline` plays a `LineCategory.ComingOver` line (`coming_01..03`: "On my way!", "Coming!", "Coming right over!"; en/pl text, baked en clips) and calls no model. `TurnSpeech.ExpectClip` makes the clip's first sample the turn's first audio; `IOfflineResponder.Respond` returns the played line, and both sides are committed to memory. Unreachable targets and crisis words still go to the model.
- A found `find_object` result says `"looking":true` and `"next"` (`FindObjectTool.NextStep`), so the model no longer adds a `look_at` round.
- Self-test `voice_come_here` reports `llm_request` and degrades when a model request was sent or release → first audio is over 600 ms.

### Hedged first event (ADR-0038)
- `FirstEventHedge`: a voice or vision Responses request with no first event after `voiceHedgeSeconds` (2.5 s), or a fast retryable failure, gets an identical second request (same body and `prompt_cache_key`); the first leg to open wins and the other is aborted and settled with an input estimate. Both share the 4 s first-event deadline. The hedge reserves its own budget and is skipped when the cap refuses. `voiceAttempts` 2 → 1 (worst-case silence ~8 s → 4 s). Summarisation is unchanged.
- SSE streams report time to first byte and first event (`IHttpTransport.StreamSse` first-byte callback, `MetaSseStream.TimeToFirstByte` / `TimeToFirstEvent`); logs `evt=llm.hedge` and `evt=llm.first_event`. A first-event timeout says "no byte" or "first byte at N ms".
- Test: a stall on both legs ends in the hold line and the spoken Error line, end to end through the real `AgentLoop` and hedged `ResponsesChatModel` (`BothHedgeLegsStall_TheHoldLineThenTheErrorLineAreSpoken`).

### Dark-room vision guard (ADR-0039)
- `FrameLuma.Mean` on the grabbed frame: below `JollyConfig.visionMinMeanLuma` (25) `capture_and_identify` and `find_object` return `too_dark` without a SAM or Spark call, log `evt=vision.too_dark` and keep the last bright capture for `highlight_part`; a prompt rule has the model ask for light in one sentence. The self-test's still switch uses the same helper and threshold.

### Acknowledgement only for questions (ADR-0040)
- The Acknowledge clip ("Hmm!", "Good question.") is no longer queued at PTT release on every turn. It plays at the final, and only when `QuestionCue` says the final is a question (a '?' or a question opener after leading fillers; never a polite request like "can you…") and no guard handled it (stop, come walk, crisis). The demo's "Nubi, come here." now gets only its ComingOver line (it was "Good question. On my way!" on the headset). `ConversationTimings.AcknowledgeOnRelease` / `JollyConfig.acknowledgeOnRelease` → `AcknowledgeQuestions` / `acknowledgeQuestions`; `question.openers|requests|fillers` fallbacks in en and pl.

### Point and tap to move (ADR-0044)
- A small ring lies on walkable floor under the free hand ray (or a Touch controller's ray). With it showing, a pinch released within 0.25 s (`JollyConfig.tapMaxSeconds`) sends the character there; held longer it talks. A table top, couch seat or box top sends it to the floor beside that edge. The trigger on the floor moves; A/X still talk. The walk ends facing the user, and a tap during a reply moves without interrupting it.
- `TapHoldClassifier` (a pinch with no ring talks at once, so barge-in keeps 150 ms), `PreRollCapture` (the microphone starts at the pinch; a hold hands its audio to the turn, a tap drops it unheard: no ASR session, no model request, no spend), `MoveTargetResolver`, `PointerTargeting`, `PointToMove` (`evt=move.request`, `evt=move.arrived`), `XrPointer`, `MoveReticleView` with the `Jolly/MoveReticle` shader, `TouchBindings` shared with `ControllerPushToTalk`.
- Self-test scenario `tap_to_move`: a scripted floor ray and a 100 ms pinch must walk the character there with no conversation state change and no spend.

### Shell fur (ADR-0045)
- `Jolly/Character/ShellFur`: an opaque base layer and alpha-tested shells (normal push, gravity droop, comb flow, inertia swing, idle sway; per-vertex colour and light, one noise read per shell fragment), environment-depth occlusion and alpha = visibility like NubiSkin, SRP Batcher and single-pass instanced stereo.
- `ShellFur` appends the shells as extra materials on a renderer whose last submesh is fur, so they reuse the vertices skinned once; three shell counts by the user's distance (`ShellFurLod`, 0.15 m band) and a spring on the root bone for the tips (`FurInertia`). `NubiCharacterController` attaches it at `Construct` and ticks it in `LateUpdate`.
- `build_jolly.py` puts the gloss slot and faces first, so the local Jolly skin's fur is its last submesh.
- `tools/device/selftest.sh` without `--apk` installs the newer of `JollyXR/Builds/Android/Nubi.apk` and `artifacts/Nubi.apk` and says which.

### Docs
- Demo script: mandatory problem voice-over, cost as a beat-5 overlay, a longer beat 2, reboot and greeting warm-up before beat 1, beat 1 as the guard walk with a canned line (ADR-0037, ADR-0040). README "A day with Nubi", "Living with it", current status and test counts; release checklist verification block for `36778c1`; device self-test reports for every change above (`docs/perf/selftest-report.md`).
