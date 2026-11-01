# Scope and build plan

Deadline: **2026-11-04 21:00 ET**. Internal freeze is 2026-11-04 15:00 ET (video edit and submission buffer).
Rule: when time runs short, cut a *could*, then a *should*, and never cut quality (tests, error paths, perf) on a *must*.

## 1. Must, should, could (from critic §5, re-scored with verified facts and the judge review)

Hero user: an older adult living alone (`hackathon.md` §2). Demo objects serve that user: a TV remote, a moka pot or a thermostat, never medication.

| Tier | Feature | Wow | Risk | Notes |
|---|---|---|---|---|
| Must | MR scene, hand tracking, Nubi idle + look-at + blink | med | low | |
| Must | Pinch PTT → Muse realtime ASR → Spark (Responses, streaming, tools) → Wit TTS → lipsync + captions, with `TranscriptAliases` | high | med | Wit verified live (TTFB 186–435 ms). WS on IL2CPP passed the device spike transport run; latency R4 still open |
| Must | `capture_and_identify`: pre-captured PCA frame → parallel SAM 3.1 mask + streamed Spark vision answer → mask overlay in world | **very high** | high | R1. Vision `minimal` first text 1.5 s (live) |
| Must (if R1 passes) | Hand-ray point → SAM point prompt, with hand-joint **negative** points; noun-phrase retry when the mask is implausible | high | med | Same `CameraMath` as the overlay, run in reverse. Falls back to centre point + hint if R1 fails |
| Must | `come_here` / `follow_user` / `look_at` / `stop` on a runtime NavMesh of the scanned floor; local `StopGuard` | high | med | |
| Must | Pinch barge-in (cancel tree, UWR abort, flush) | med | low | |
| Must | Tool badges + latency HUD | med | low | How judges see the Meta tooling in the video |
| Must | Offline/degraded mode: health probe, canned lines, idle scheduler | med | low | |
| Must | Personas as ScriptableObjects (Companion, Mentor, Guru) | med | low | |
| Must | Usage meter with a hard cap; secrets via file; `store:false`; LiveUser → standard model | — | low | |
| Must | Tests green in batchmode, IL2CPP APK, README/THIRD_PARTY/CHANGELOG/ADRs, demo video | — | low | |
| Should 1 | `find_object(noun_phrase)`: "where are my glasses?" → SAM text prompt highlights every instance, Nubi looks/walks to the nearest | **very high** | low | Reuses the capture pipeline; $0.0025 per call |
| Should 2 | Scene memory + `highlight_part` follow-ups ("which part do I hold?") | high | low | "mug handle" part phrase verified live |
| Should 3 | Depth-only occluders from AR planes/boxes | med | low | Nubi hidden behind real furniture |
| Should 4 | `set_persona` by voice (greets in the new voice); Psychologist persona with crisis guard (built, not demoed) | med | low | |
| Should 5 | `remember_place` + `walk_to(place)` with persisted anchors | med | med | |
| Should 6 | `play_gesture`, moods, acknowledgement clip | med | low | Demo polish |
| Should 7 | Conversation memory summarization + persistence | med | low | |
| Could | Polish ASR + captions (TTS stays baked lines) | low | med | |
| Cut | Voice barge-in / ENDPOINTING hands-free, wake word, LAN Glimmer, in-headset key entry, Muse Image props, Polish beyond captions | — | — | Post-hackathon backlog |

## 2. Spikes

`Assets/Spikes/DeviceSpike` already bundles R1 (camera probe), R2 (Wit TTS), R4 transport (ASR WS) and R5 (mic) into one headless APK that logs parsable `SpikeLog.Result` lines (`tools/device/*.sh`). One human headset session on the morning of Nov 2 must close every spike: run it in a lit room with `run_paid` present.

| Spike | Owner WP | Pass criterion | Fallback |
|---|---|---|---|
| R1 PCA → SAM → world point, + capture/cast | WP5 | Sphere within 3 cm of a marker at 1 m; capture → mask < 2 s; **the Quest system recorder and casting (or scrcpy / MQDH cast) capture passthrough + app layers while the camera is in use** | `WebCamTexture` source; centre-point prompt only; scrcpy for the video |
| R2 Wit TTS streaming on IL2CPP + flush | WP7 | First audio ≤ 500 ms after request on Quest; **pinch → silence ≤ 150 ms with the `OnAudioFilterRead` output** | Desk-verified 186–435 ms TTFB; fallback is baked lines only |
| R3 Nubi rig + mouth + uLipSync on Quest | WP6 | 72 fps with passthrough; mouth reads on camera; uLipSync after the speech filter on one GameObject | Amplitude driver; sprite mouth |
| R4 `ClientWebSocket` on IL2CPP + end-to-end latency | WP3 | p50 PTT release → first answer audio ≤ 2.0 s over 20 turns | SSE file ASR endpoint |
| R5 Mic soak 15 min + Wi-Fi toggle | WP3/WP10 | No freezes; offline line ≤ 2 s | Restart the mic session per turn |
| R6 Vision/SAM evaluation on the demo room (budget use, ≈ $5) | WP4 | Report in `docs/perf/vision-eval.md`: SAM hit rate for centre point vs hand point (+ negatives) vs noun-phrase retry; answer quality and latency for spark-1.3 minimal vs low vs `muse-wonder-1.0`, on 30–50 frames of the hero objects | Keep `minimal` + centre/hint prompts |

R6 frames are captured only in a Development build with the `JOLLY_DEBUG_CAPTURE` define (excluded from release; DoD greps for it), of the developer's own room, and sent only on the LiveUser (standard) route because they are real home imagery. ≈ 200 calls, logged in `docs/api-spend.md`.

## 3. Work packages

Ownership is by **file path**. Two WPs never edit the same file. `Jolly.Contracts` is frozen after WP0; a change goes through the orchestrator together with an ADR note. Each WP must leave `tools/ci/test.sh` green and add `CHANGELOG.md` lines only under its own `### WPn` bullet in the Unreleased section.

| WP | Title | Owns (files / asmdef) | Consumes | Provides | Tests required | Est. | Depends on |
|---|---|---|---|---|---|---|---|
| **WP0** | Scaffold | Unity project `JollyXR/` (URP, Android, Meta Quest build profile, project settings per `xr-stack.md` §6), `Packages/manifest.json` (pins per ADR-0002, plus OpenUPM `com.cysharp`, `com.unity.animation.rigging`, and the uLipSync git URL pinned to `ece3272`), **all asmdefs** (empty folders + `.asmdef` + a `csc.rsp` with `-warnaserror+` beside each), the per-asmdef `link.xml` stubs, `Runtime/Contracts/**` verbatim from `contracts.md`, `Runtime/Audio/Common/**` (`SpscFloatRing`), `Tests/Shared/Jolly.Testing.asmdef` + `Tests/Shared/Fakes/Common/**` (`FakeClock`, `InMemorySecrets`, `FakeRandom`, `RecordingLog`), `Tests/EditMode/Architecture/**` (asmdef-graph test), `tools/ci/test.sh` (incl. the `CancelAfter(` and `JOLLY_DEBUG_CAPTURE` greps), `tools/ci/build.sh` (wrapping `Jolly.Editor.Build.QuestBuild.BuildApk`), `Scenes/Main.unity` with XR Origin (XRI Starter Assets rig) + an empty `JollyBootstrap` | — | Compiling skeleton; green CI; IL2CPP APK that shows passthrough; one deliberate warning proves `csc.rsp` is honoured, then is removed | 1 EditMode smoke test, 1 PlayMode smoke test, asmdef graph, `SpscFloatRing` wrap/underrun | 3–4 h | — |
| WP1 | Net + Responses chat model | `Runtime/Net/{Http,Sse,Retry}/**` (incl. `RetryPolicy`), `Runtime/Providers/MetaApi/{Common,Chat}/**`, `Runtime/Core/Usage/**`, `Tests/EditMode/{Net,Chat,Usage}/**`, `Tests/Fixtures/sse/**`, `Tests/Shared/Fakes/WP1/**` | `IApiUsageMeter`, `ISecretsProvider`, `ILog`, `IClock` | `IChatModel` (`ResponsesChatModel`, incl. `UserImage` → `input_image` data URL), `HttpTransport`, `SseDownloadHandler` (preallocated buffer), `RetryExecutor`, `ApiUsageMeter` (reservations, estimates, `Flush`), MetaApi `Common` (auth, base URL, status mapping, `MetaModelResolver`) | SSE parser (split chunks, CRLF, `[DONE]`, multi-line data), Responses reducer from recorded fixtures (text, commentary + function_call, parallel calls, `response.failed`), retry policy, usage cost math + reservation + abort estimate, cancellation aborts, resolver throws on contributor + LiveUser | 5 h | WP0 |
| WP2 | Core conversation | `Runtime/Core/{Conversation,Agent,Tools,Memory,Personas,Secrets,Diagnostics,Config}/**`, `Content/Localization/Tables/Crisis*`, `Tests/EditMode/Core/**`, `Tests/Shared/Fakes/WP2/**` | All Core-facing contracts | `ConversationStateMachine`, `ConversationController` + `TurnRouter` + `TurnPipeline` + `BargeInCoordinator`, `AgentLoop`, `ToolRegistry` + schema validator, **all catalogue tools** incl. `find_object`/`highlight_part` (against interfaces), `ConversationMemory`, `SceneMemory` + DTOs, `AtomicFile`, `PersonaRepository`, `CrisisGuard`, `StopGuard`, `TranscriptAliases`, secrets providers, `LatencyTracker`, `RedactingLog` | State table exhaustive incl. latched flags and every watchdog; agent loop multi-round, parallel, locomotion supersede, Exclusive not waiting for Locomotion, `EndsTurn`, 4-round cap, invalid args, cancel commits interrupted outputs; persona switch applied only when Idle; each tool with `RecordingCharacterController`; memory caps, atomic first save and reload round-trip; secrets parsing; redaction; `ToolResult.JsonString` control chars | 8 h | WP0 |
| WP3 | Voice input | `Runtime/Audio/{Capture,Resampling}/**`, `Runtime/Providers/MetaApi/Asr/**`, `Runtime/Net/WebSockets/**`, `Tests/EditMode/{Audio,Asr}/**`, `Tests/Fixtures/asr/**`, `Tests/Shared/Fakes/WP3/**` | `IAudioCapture`, `ISpeechToText` | `UnityMicrophoneCapture`, `Resampler` (48k→24k), `MuseRealtimeSpeechToText` + SSE-file fallback | Resampler accuracy; handshake JSON; event parsing (partials, final, error, close codes); backlog sent at once after the ack, live pacing ≤ 5 s ahead, `endStream` within one frame of source completion (`FakeClock`) | 5 h + R4/R5 on device | WP0 |
| WP4 | Vision | `Runtime/Providers/MetaApi/Vision/**`, `Runtime/Core/Imaging/**`, `Tests/EditMode/Vision/**`, `Tests/Fixtures/{sam,images}/**` + `ATTRIBUTION.md`, `docs/perf/vision-eval.md` | `ISegmenter`, `IObjectIdentifier`, `IImageEncoder`, `IChatModel` | `SamSegmenter` (point/negative-point/text prompts), `SamOutputParser`, `OneBitMaskDecoder` (C# port with the SAM License header and `LICENSE-SAM.txt`), `SparkObjectIdentifier` (streamed `LABEL\|PHRASE` header + answer deltas), `JpegImageEncoder`; R6 evaluation | Grammar parser (boxes inclusive, multi-object, empty output); one_bit decode matches the reference coverage 0.109 / 0.724 from `meta-model-api.md` §4.3 fixtures; prompt token formatting incl. negative points (`w`/`h` must match); header parser incl. missing `\|` and split across deltas | 6 h + R6 | WP0, WP1 (HTTP) |
| WP5 | XR layer | `Runtime/XR/**` (incl. `Interaction/**` detectors, `SceneOccluders`, `EditorStillFrameSource`), `Runtime/XR.MetaQuest/**`, `Runtime/Core/Spatial/**` (`CameraMath`), `Plugins/Android/**`, `Editor/Build/**` and the OpenXR/XR settings in `ProjectSettings/**` after WP0, `Tests/EditMode/Spatial/**`, `Tests/PlayMode/XR/**`, `Tests/Shared/Fakes/WP5/**` | `ICameraFrameSource`, `IUserPoseProvider`, `IWorldQuery`, `ISpatialAnchors`, `IVoiceActivation`, `IInteractionSignals` | `PermissionGate` (camera manager on callback; scene managers on `USE_SCENE`), `PinchPushToTalk` (UI-hover and palm-up gating), `XrUserPoseProvider` (incl. hand key points), `ArWorldQuery`, `ArSpatialAnchors`, `FloorNavMeshBuilder` (+ `NavMeshModifier` on boxes), `MetaPassthroughFrameSource`, `Camera2Extrinsics`, gaze/palm/poke detectors; `QuestBuild.BuildApk` calls `AddressableAssetSettings.BuildPlayerContent()` | `CameraMath` pixel ↔ ray round trip in EditMode; NavMesh build on a simulated floor in PlayMode (box tops not walkable); **R1 on device** | 8 h (incl. R1) | WP0 |
| WP6 | Character | `tools/blender/**`, `Assets/Jolly/Art/Character/**`, `Content/Character/Nubi/**`, `Runtime/Character/**`, `Runtime/Integrations/ULipSync/**`, `Tests/PlayMode/Character/**` | `ICharacterController`, `ILipSyncDriver` | `Nubi.fbx` (CC0, exists), Animator controller, gesture clips, `NubiCharacterController` (`TryStartMoveTo`/`WhenArrived`/`Mode`), look-at/blink/mouth/mood drivers, `ULipSyncDriver` | Move arrives / unreachable / superseded; cancelling the `WhenArrived` token does **not** stop the agent; gesture completes; mouth weights follow a test tone; Animator params set | 9 h + R3 | WP0 |
| WP7 | Voice output | `Runtime/Providers/Wit/**`, `Runtime/Audio/{Output,LipSync}/**`, `Runtime/Core/Speech/**`, `Editor/Tools/CannedLineBaker.cs`, `Content/Audio/Lines/**`, `Content/Localization/Tables/{Lines,LineClips}*`, `Tests/EditMode/Speech/**`, `Tests/Shared/Fakes/WP7/**` | `ITextToSpeech`, `ISpeechOutput`, `ILipSyncDriver`, `ICannedLines` | `WitTextToSpeech` (preallocated handler), `StreamingSpeechOutput` (`OnAudioFilterRead` + ring + resample), `AmplitudeLipSyncDriver`, `SentenceSplitter`, `SpeechQueue`, `CannedLines`, baked en lines | Splitter cases (abbreviations, numbers, 240 cap, `。`); Wit chunker odd byte; `PcmChunk` double dispose is a no-op; queue prefetch + clear on barge-in; `Flush` cancels pending `Play`; inter-chunk timeout; FirstSamplePlayed ordering | 6 h + R2 | WP0 |
| WP8 | Presentation + localization | `Runtime/Presentation/**`, `Runtime/Localization/**`, `Content/Localization/{Locales/**,Tables/UI*}`, the Addressables settings asset (`BuildWithPlayer`), `Content/Config/LocaleVoiceMap.asset`, `Tests/PlayMode/Presentation/**` | `ILocalizer`, `IConversationEvents`, `IUserPreferences` | `UnityLocalizer` (preload + sync get, unsubscribes on dispose), `CaptionView`, `MaskOverlayView` (`IMaskPresenter` + outline shader), `ToolBadgeView`, `LatencyHud`, `StatusIndicator` (`IStatusSink`), `SettingsPanel` (`IUserPreferences`: text size, seated, speech rate, volume, PTT hand, persona, language, clear memory) | Localizer fallback to key; caption timing; overlay places a quad at a given pose/size | 6 h | WP0 |
| WP9 | Integration | `Runtime/App/**`, `Content/Personas/**`, `Content/Localization/Tables/Personas*`, `Content/Config/JollyConfig.asset`, `Scenes/Main.unity` (after WP0), `Editor/Tools/Validators/**`, `Tests/PlayMode/E2E/**` | everything | `JollyBootstrap` (disposable list, pause flush), persona assets + prompts, wiring of detectors → `InteractionTriggers`, persona and asset validators | **E2E PlayMode**: scripted voice turn (state sequence, captions, tool calls), barge-in ≤ 150 ms, capture_and_identify with a still frame + scripted SAM/vision, offline path, `NoGcAllocInSteadyStateIdle` | 6 h | WP1–WP8, WP10 |
| WP10 | Offline + connectivity | `Runtime/Core/{Connectivity,Offline}/**`, `Runtime/Providers/MetaApi/Health/**`, `Tests/EditMode/Offline/**`, `Tests/Shared/Fakes/WP10/**` | `IConnectivityMonitor`, `IHealthProbe`, `ICannedLines`, `ICharacterController`, `IInteractionSignals` | `ConnectivityMonitor` (5 states, probe off without key), `MetaHealthProbe`, `OfflineResponder : IOfflineResponder`, `IdleScheduler`, `InteractionTriggers` (rate limiter, pure) | State transitions with `FakeClock`; backoff; no repeat in the last 5; rate limit 1 per 20 s; idle only when Idle and not moving/following | 4 h | WP0 |
| WP11 | Build, perf, docs | `tools/ci/**` (after WP0), `README.md`, `THIRD_PARTY.md`, `CHANGELOG.md` (structure), `docs/perf/**` (except `vision-eval.md`), `docs/demo-script.md` | — | IL2CPP release APK, OVR Metrics captures, perf report, README theme mapping (`hackathon.md` §2), THIRD_PARTY (fixed versions per C1, + UniTask, uLipSync, glTFast, Newtonsoft, SAM License) | DoD checklist run | 5 h | WP9 |
| WP12 | Demo | `docs/demo-script.md` rehearsal log, video project (outside the repo) | device | ≤ 2:45 video (6 beats, `definition-of-done.md` §6), 10 consecutive clean live runs of the required beats | — | 6 h | WP11 |

### Parallelism and order

```
Nov 1  WP0 (serial, ~4 h) ──► WP1 ∥ WP2 ∥ WP3 ∥ WP5 ∥ WP6 ∥ WP7 ∥ WP8 ∥ WP10
Nov 2  AM: ONE headset session runs the existing DeviceSpike APK → closes R1, R2, R4, R5 (+ capture/cast check)
       Thin end-to-end slice: JollyBootstrap wired with fakes + real ASR → LLM → TTS on device (WP9 starts early, owns the wiring)
       WP4 (needs WP1 transport) ∥ continue the above; R3 and R6 by end of day
Nov 3  WP9 full integration + on-device fixes; should-tier features in priority order
Nov 4  WP11 perf/docs ∥ WP12 rehearsal + video; freeze 15:00 ET; submit by 18:00 ET
```

Shared-file protocol:
- The only files touched by several WPs are `Scenes/Main.unity` and `JollyConfig.asset`, both owned by WP9 after WP0. Other WPs ship prefabs, which WP9 places.
- Localization: one string table per owner (Crisis → WP2, Lines + LineClips → WP7, UI + locales → WP8, Personas → WP9).
- `link.xml`: one per asmdef folder, owned by that folder's WP; Unity merges them.
- Fakes: `Tests/Shared/Fakes/WPn/**` per WP; fakes needed by several WPs live in `Fakes/Common` and are created in WP0 (request additions through the orchestrator).
- `CHANGELOG.md`: append-only bullets per WP.
- `Packages/manifest.json`: WP0 only. Request additions through the orchestrator.

### Human actions needed

1. Headset in developer mode, with Space Setup done in the demo room. Morning of Nov 2: one session running the DeviceSpike APK in a lit room (closes R1, R2, R4, R5 and the capture/cast check).
2. `adb push` of the secrets (ADR-0010).
3. Confirm the official problem-statement wording for the README (critic B7).
4. Video recording and voice-over.
5. Live-final logistics (critic C15).
