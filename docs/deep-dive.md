# Deep dive

The detail behind the [README](../README.md): how a day with the companion looks, the controls, the engineering, every measured number, every known limit and the roadmap. Character names: the repository ships **Nubi**; the demo video and the live sessions behind ADRs 0041–0051 ran the **Jolly** look-alike skin on the same code (ADR-0052).

## Contents

- [A day with it](#a-day-with-it) · [Features beyond the main demo](#features-beyond-the-main-demo) · [Controls](#controls)
- [Technical depth](#technical-depth) · [Performance budget](#performance-budget) · [Known limitations](#known-limitations)
- [Accessibility](#accessibility) · [Localization](#localization) · [Roadmap](#roadmap)

---

## A day with it

In the morning the TV remote won't do what you expect. You hold it up and ask "what is this, and which button turns it on?": Nubi outlines the remote, names it, and `highlight_part` outlines the power button on the same capture (PlayMode [`CaptureAndIdentify_SegmentsTheStillFrameAndSpeaksTheVisionAnswer`](../JollyXR/Assets/Jolly/Tests/PlayMode/E2E/E2ETests.cs) and EditMode [`HighlightPart_UsesTheLastCaptureWithoutANewOne`](../JollyXR/Assets/Jolly/Tests/EditMode/Core/Tools/FindAndHighlightToolTests.cs); `highlight_part` passed on the headset in the [`6f0383c` run](perf/selftest-report.md#sam-text-prompt-tools-under-the-self-test-hand-debounce-6f0383c)). Later, "where are my glasses?": `find_object` outlines every match in view and Nubi looks at the nearest one ([`FindObject_OutlinesEveryInstanceAndLooksAtTheNearest`](../JollyXR/Assets/Jolly/Tests/EditMode/Core/Tools/FindAndHighlightToolTests.cs); the model chose `find_object` for "where is my mug?" in 3 of 3 [`36778c1` runs](perf/selftest-report.md#local-come-here-guard-and-reply-log-36778c1)). Standing by the sofa, "remember this place as the sofa" saves the spot with a spatial anchor, and "walk to the sofa" sends Nubi back there later ([`RememberPlace_SavesTheUsersFloorPositionWithAnAnchor`](../JollyXR/Assets/Jolly/Tests/EditMode/Core/Tools/CharacterToolTests.cs), [`WalkTo_TaughtPlace_StartsMotionAndReturnsAtOnce`](../JollyXR/Assets/Jolly/Tests/EditMode/Core/Tools/LocomotionToolTests.cs); EditMode only, not yet tried on a headset). When the Wi-Fi drops in the evening, "are you there?" still gets a spoken, lip-synced answer from a baked offline line ([`Offline_PressPlaysACannedLineWithoutTheCloud`](../JollyXR/Assets/Jolly/Tests/PlayMode/E2E/E2ETests.cs); `offline_greeting` passed in 4 of 4 [`3e99cb5` runs](perf/selftest-report.md#cartoon-voice-merged-character-voices-device-regression-fixes-3e99cb5)).

### Living with it

- **What a day costs.** Take 30 voice turns, 5 of them "what is this?" questions:
  - An average turn cost **$0.0042**: $0.0845 ÷ 20 for the 20-turn device run on `751acba`, which rotated greetings, "come here" and vision questions (6 of the 20) through ASR, Spark with tools, SAM and Spark vision ([report](perf/selftest-report.md#stale-frames-are-device-state-not-the-build-or-heat-20-turn-latency-751acba)). 30 × $0.0042 = **$0.13**.
  - As a ceiling, price the 5 vision calls again on top at their worst case, **≤ $0.011** each: Spark vision ≤ 1.0k input tokens × $1.25/M = $0.0013 (~0.8–1.0k on the vision probes in [`api-spend.md`](api-spend.md)), ≤ 1,024 output tokens (`visionMaxOutputTokens`) × $4.25/M = $0.0044, and 1–2 SAM images (a noun-phrase retry) × $0.0025 = ≤ $0.005. Prices: [`api-spend.md`](api-spend.md).
  - Day: $0.126 + 5 × $0.011 = **≤ $0.18**, about $5.40 for 30 days. The default per-headset hard cap (`JollyConfig.hardCapUsd`, $20) covers over 100 such days before Nubi falls back to its offline lines. Cross-check: a default self-test run (six online voice turns, three of them vision tools) costs $0.020–0.028 on the app's meter ([`ee9b05c` runs](perf/selftest-report.md#hedged-first-event-ee9b05c-b96a062)).
- **How it is meant to be used.** Short moments: put the headset on, hold up the remote or the moka pot, ask, hear the answer, take it off. A self-test run of 13 scenarios takes about 3 minutes. Nubi is not designed for hours of wear.
- **Who sets it up.** A family member, once: Space Setup in the room Nubi lives in, the release APK installed, and their own API key pushed from a computer (`push-secrets.sh`; the Meta ToS forbids sharing a key, so each household brings its own; [Build and run](../README.md#build-and-run)). After that the person at home only holds a pinch and talks.
- **The headset is the prototype, not the product.** Wearing a Quest 3 every morning is not what an older adult living alone will do. Glasses are the everyday form factor, and the brain is built to move there unchanged ([Why Quest today, glasses tomorrow](#why-quest-today-glasses-tomorrow)).

## Features beyond the main demo

- `find_object` ("where are my glasses?") highlights every match.
- `remember_place` and `walk_to("sofa")` use persisted anchors.
- **Your real furniture.** Nubi knows the couch, table, bed and chairs from Space Setup: "go sit by the couch" walks it to the couch's edge, facing it, and it looks at things in the room when it talks about them. Without Space Setup it says so once, stays within about 1.5 m of you, and opens Space Setup when you ask (ADR-0025).
- `set_persona` switches personas by voice.
- Gaze, open-palm and poke greetings (not yet confirmed with a person wearing the headset).

The full tool catalogue is in `JollyXR/Assets/Jolly/Runtime/Core/Tools`.

## Controls

Hands are the primary input; Touch controllers work too. Your tracked hands are drawn over passthrough as translucent hand meshes and disappear when tracking is lost; picking up a controller swaps that side to the controller model and its ray, and with the headset's automatic switching on, putting the controllers down brings your hands back (XRI Hands Interaction rig, [ADR-0030](decisions/0030-hands-interaction-rig.md)).

| | Hands | Touch controllers |
|---|---|---|
| **Talk** (hold, release to send) | Pinch thumb and index finger and hold | Hold **A** (right) or **X** (left) |
| **Send Nubi somewhere** | Point the hand ray at the floor (a small ring appears) and pinch briefly | Point the ray at the floor and pull the trigger |
| **Interrupt Nubi** | Pinch (a press silences it at once, ring or no ring) | Press A or X again |
| **Settings, buttons** | Point the hand ray and pinch, or poke the button with your index finger | Point the ray and pull the trigger |
| **Poke / greet Nubi** | Poke with the index finger, or hold an open palm toward Nubi | |

- **A pinch does not talk** while that hand's ray is on a button, or while your palm faces you (the Horizon OS menu gesture). Pinching while you point at Nubi does talk: the ray doesn't select Nubi, only a finger poke does. Settings → *PTT hand* limits pinch-to-talk to your dominant hand.
- **Settings appear only when you ask** (ADR-0042): turn your left palm toward you and press the small *Settings* button on the wrist, press ☰ on the left controller, or say "open settings". The panel opens in front of you and stays put; its X, a talk pinch, "close settings" or 20 s untouched close it.
- **The trigger never talks.** On the floor it moves Nubi; on a button it presses the button. It belongs to XRI's Activate action and UI press, and a runtime can report a hand pinch as a trigger; binding talk to it would skip the pinch checks above. The grip stays XRI's Select. A and X are bound to the Touch controller layouts by name, so no hand input can press them ([ADR-0021](decisions/0021-controller-push-to-talk.md)).
- **Tap to move, hold to talk** ([ADR-0044](decisions/0044-point-and-tap-to-move.md)). While the ring shows, a pinch shorter than 0.25 s sends Nubi there; hold it longer and you are talking. The microphone starts at the pinch, so a hold loses no words; a tap's audio is dropped on the headset and never sent to ASR or the model. With no ring (pointing at the air, a wall, a button or Nubi) a pinch talks at once, as before. The ring shows only on floor Nubi can walk to, and not within 0.5 m of your feet; pointing at a table top, couch seat or box top sends Nubi to the floor beside it, on the side you pointed at. Nubi turns to you when it arrives.
- **A pinch silences a busy turn at the press.** While Nubi talks or moves, a pinch stops the talking at once. A quick pinch at a ring then also walks there; a hold then talks.
- While a hand is tracked, that side's controller button and trigger are ignored, which is the same rule XRI's input modality manager uses.
- Holding a pinch and a button together counts as one press, so it can't interrupt your own turn.

## Technical depth

- **Barge-in cancellation tree.** `appCts` → `sessionCts` (persona/locale) → `turnCts` (LLM stream, TTS requests, tools, ASR socket). A pinch cancels the turn first and then flushes audio with a 20 ms fade. Locomotion runs outside `turnCts`, so an interruption stops the talking, not the walking. `UnityWebRequest` aborts on the PlayerLoop.
- **Streaming audio output without `AudioClip`s.** `StreamingSpeechOutput` is a clip-less `AudioSource` whose `OnAudioFilterRead` pulls 24 kHz PCM from a ring and resamples it to the device rate. `Flush()` silences exactly what is playing, and "first sample played" is measured where the sample is actually consumed, not where it is read ahead. uLipSync sits after the filter on the same GameObject, so the mouth follows the audio you hear.
- **Lock-free SPSC ring** (`SpscFloatRing`) between the main thread and the audio thread: `Volatile` read/write, no locks and no allocations in the audio callback.
- **Allocation discipline.** Pooled 32 KB `DownloadHandlerScript` buffers for SSE and Wit (the parameterless constructor allocates per chunk), `ArrayPool`-backed `PcmChunk`s with idempotent dispose, reader-based JSON on hot paths, one persistent native camera buffer, and no LINQ, closures or string concatenation in `Update` or audio callbacks. The target is 0 B/frame at steady idle, enforced by the PlayMode test `NoGcAllocInSteadyStateIdle` on the real scene (no main-thread `GC.Alloc` sample over 120 idle frames); on the headset, 30 s of idle ran with 0 GC collections.
- **IL2CPP WebSocket.** The BCL's managed WebSocket works on Quest IL2CPP (measured: 469–652 ms cold TLS connect, 150 ms warm). The upgrade is done by our own code over `SslStream`, then `WebSocket.CreateFromStream`: Mono's `ClientWebSocket` read the upgrade response one byte per TLS read, at 16 KB of garbage each (ADR-0023). Receives resume on the main thread before writing to the channel, so every provider stream yields on the main thread.
- **Passthrough Camera Access.** The meta-openxr camera subsystem gives a 1728² YUV CPU image, converted straight to 640 px in 12–14 ms (75 ms at full resolution). Intrinsics match Camera2 `LENS_INTRINSIC_CALIBRATION` exactly. Extrinsics are read from Camera2 `LENS_POSE_*` over JNI (they are gyroscope-relative). Image timestamps are `CLOCK_MONOTONIC`, and the head pose is interpolated at that timestamp. The pixel ↔ world math (`CameraMath`) is EditMode-tested. Source: `docs/research/device-spike.md`.
- **Real-world occlusion without an extra draw.** AR Foundation's `ARShaderOcclusion` publishes Quest's per-eye environment depth; Nubi's own shaders reproject each fragment into it and write premultiplied colour with alpha = visibility, so a fragment behind your couch or your hand ends transparent and the passthrough shows through. Soft edges come from AR Foundation's depth preprocess. Without depth (unsupported runtime, stalled provider), boxes from Space Setup bounding boxes take over, drawn after Nubi and before the UI so they never hide captions or panels. The boxes need the same `USE_SCENE` permission, so with it denied nothing hides Nubi. The contact shadow fades and shrinks as both feet leave the floor (ADR-0024).
- **Runtime NavMesh sized for a living room.** Floor plane outlines are triangulated into mesh colliders, and bounding boxes are baked as Not Walkable (`NavMeshBuilder.BuildNavMeshData`, agent radius 0.15 m). `NavMeshSurface` would use the 0.5 m Humanoid agent, which erases a living room (ADR-0017). Rebuilds are throttled to once per 2 s.
- **Table-driven conversation state machine.** Three latched flags and per-state watchdogs. It is tested exhaustively, including every barge-in edge.
- **Safety and robustness.** `CrisisGuard` runs before the model, for every persona, and hands off deterministically. A persona's tool allow-list is enforced when a call executes, not only in the list sent to the model. `StopGuard` stops Nubi locally, and `ComeGuard` starts the walk on a short "come here" before the model (ADR-0036). Retries happen only before the first streamed byte, with full jitter and `Retry-After` capped at 8 s; a voice request still silent after 2.5 s is raced by an identical hedge request instead of retried after its timeout (ADR-0038). A five-state connectivity monitor (Online, Degraded, Offline, Unauthorized, BudgetReached) runs a backed-off health probe.
- **Quality gates** (`tools/ci/test.sh`; the static ones also run on every push through `tools/ci/gates.sh`): `-warnaserror+` on every assembly; no `async void`; no `System.Threading.Tasks.Task` (UniTask everywhere, except the two `Stream` subclasses whose base methods return it); no `CancellationTokenSource.CancelAfter` (it fires on a thread-pool timer); no debug-capture defines in shipped settings; a content validator for personas, string tables and line clips; line coverage of Core, Net, Providers and Audio at or above 91.6 % (`tools/ci/coverage.sh`). Current results: [README](../README.md#results).
- **A self-checking pipeline on the headset.** `tools/device/selftest.sh` installs the release APK, arms a built-in self-test and runs 13 scenarios with nobody wearing the headset. Bundled voice clips go through the real microphone pipeline to Muse ASR, Spark (with tools), SAM, Wit and the speaker. It also forces the network off and on, barges in, and samples frame time and render counters. It writes a PASS/DEGRADED/FAIL table and the run's API cost ([`docs/perf/selftest-report.md`](perf/selftest-report.md), ADR-0018). It found bugs that no desk test could, including a health-probe URL that kept the app offline, a vision turn that ended in silence, and a new tool whose description stole "where is my mug?" from `find_object`.

## Performance budget

Targets come from [`docs/definition-of-done.md`](definition-of-done.md) §4–5. Stale-frame, memory and voice-latency rows come from `751acba` (cooled and rebooted headset, 20-turn run; report section "751acba"); the other measured values come from the on-device self-test of the full app on `main` at `3e99cb5` (Quest 3, Horizon OS v209, IL2CPP release build, three runs of the default build plus one of the Jolly build on 2026-11-01; [`docs/perf/selftest-report.md`](perf/selftest-report.md), section "3e99cb5"), in a dim room with no Space Setup and nobody wearing the headset. Pinch → silence pools the `751acba`, `f311081` and `36778c1` sections. Rows that name an earlier pass come from that pass's section of the same report; the self-test does not measure them. Rows marked **unmeasured** need the OVR Metrics capture or a development-build Profiler session; the self-test report keeps the full DoD coverage table. Method: [`docs/perf/README.md`](perf/README.md).

**Latest (2026-11-03, 21:01 UTC, Nubi build, rebooted headset, about 10 minutes of uptime):** overall FAIL on model latency only. Release → first audio on model turns 2.8–6.0 s, ASR final → first text 2.2–5.4 s, on-device checks all PASS, stale frames 1.71 % over the short run. The table below keeps the earlier passes it cites; see [`selftest-report.md`](perf/selftest-report.md).

| Metric | Target | Measured |
|---|---|---|
| Frame rate (passthrough, Nubi visible) | ≥ 72 fps sustained | **72.0 fps** average, p95 frame 13.9 ms (30 s idle, 4 runs) |
| Stale frames | < 1 % over 10 min | **Met on a freshly rebooted headset except at launch.** After a reboot: turns **0.49–0.60 %**, idle **0.23–0.32 %**, whole run **0.93 %** over a 242 s 20-turn run and 1.77 % over a 106 s default run, whose ~90–110 launch stale frames are most of it. Before the reboot (4 days 14 h uptime), the same APK on a headset cooled for 20 min dropped 5.0 % idle and 6.2 % over the run, and OVR Metrics already counted stale frames during the Unity splash: device state, not the build or heat (ADR-0034). On Nov 3: 5.89 % at 44 h of uptime, 1.71 % ten minutes after a reboot. The 10-minute run is not done |
| Draw calls / SetPass | ≤ 200 / ≤ 60 | **17 / 15** per frame (idle) |
| Triangles | ≤ 300k scene; Nubi ≤ 30k | **33,744** per frame (idle); Nubi 25,248 tris, 2 materials, one 1024² texture (performance pass) |
| GC alloc | 0 B/frame idle; ≤ 4 KB/frame in a turn | idle: PlayMode gate `NoGcAllocInSteadyStateIdle` passes, and 0 GC collections in 30 s on the headset; per turn in a development player (fix/turn-gc): p50 **3.9 KB/frame**, max 6.4 (`barge_in`, **over**) |
| Memory | `app_rss_MB` ≤ 1,500 MB peak | `app_rss_MB` peak **642–643 MB** (OVR Metrics; `dumpsys meminfo` RSS 643–644 MB, PSS 463 MB); system used memory 546–562 MB idle (Jolly 567 MB) |
| CPU main thread, thermal, battery | p95 ≤ 11 ms; 30 min without throttling; ≤ 20 % per 30 min | unmeasured |
| **PTT release → first answer audio** | p50 ≤ 2.0 s | p50 **1,804 ms**, p90 2,577 ms, max 2,737 ms over **20 consecutive online turns** (greeting, `come_here`, vision, repeated; no wearer, so `selftest.sh --turns 20` stands in for LiveUser). Greetings are the slow ones (p50 2,362 ms, 6 of 7 over 2.0 s); the variance is model time, request → first text p50 1,277 ms, p90 2,068 ms, max 2,445 ms |
| PTT release → ASR final | p50 ≤ 300 ms | p50 **265 ms**, max 293 ms (20-turn run) |
| ASR final → first LLM text | p50 ≤ 1.3 s | p50 **1.28 s**, p90 2.07 s, max 2.45 s (request → first text in the 20-turn run; ASR final → request ≤ 5 ms) |
| Pinch → silence | p50 ≤ 150 ms | **28–55 ms** |
| Network loss → offline line | ≤ 2.0 s | offline line captioned **13–27 ms** after the press |
| `capture_and_identify` → mask visible | p50 ≤ 2.0 s | **614–1,349 ms** (bundled still image; the room was too dark for the live frame) |
| `capture_and_identify` → answer audio | p50 ≤ 2.5 s | **1,608–2,055 ms** (4 runs); in one run the Spark vision call failed on the network and the scenario FAILed |
| Camera frame → 640 px RGBA | — | **12–14 ms** convert, frame age 31–46 ms at acquire (device spike) |

## Known limitations

- **What a wearer has and has not confirmed.** Worn sessions on the Jolly build produced the demo video and the live misses behind ADRs 0041–0051, so passthrough, hands, pinch-to-talk, walking and captions have been used on a real headset. The per-item checks were not recorded: occlusion edge quality and GPU cost, the camera extrinsics marker test (≤ 3 cm at 1 m), controller push-to-talk on the headset, and the tap time and ring size on real floors. Those remain open in [`release-checklist.md`](release-checklist.md).
- **Local guards cover short, plain phrases only.** The model's own tool choice is unreliable ([tool-selection eval](perf/tool-selection-eval.md)), so `StopGuard`, `ComeGuard`, `ActionGuard`, `SettingsGuard` and `PlaceGuard` run short requests with no model round. A guarded move has no model-authored reply, and a long sentence ("come here and tell me about this remote") still depends on the model.
- **Greetings are the slowest turn.** A greeting gets a full spoken answer and no tool: release → first audio was 3.0–3.3 s in all three `f311081` runs, 2.4–4.7 s in the `36778c1` runs, and p50 2,362 ms (6 of 7 over 2.0 s) in the 20-turn run. The time is model time (request → first text). The demo therefore opens with "Nubi, come here." and uses a greeting as its warm-up turn.
- **Without Space Setup, Nubi only knows the floor near you.** The fallback is a 1.5 m disc around the user at the tracking-origin floor height, stretched over to Nubi (up to 4 m); it does not know about a chair inside it. Re-reading planes after Space Setup by restarting the plane and box managers is unverified on the headset.
- **No perching.** Nubi stops at the edge of the couch or table and faces it; it cannot climb onto furniture (no NavMesh links or climb clips).
- **One ~283 ms frame at launch**: a single synchronous call into Meta's OpenXR provider that starts the camera and creates the passthrough layer, which app code cannot defer or split (ADR-0032). The compositor loses ~36 frames around scene start in all. Starting the camera on the scene's first frame does not hide it: the Unity splash is already on screen, and the freeze only became one longer one (measured and reverted, ADR-0034).
- **Stale frames depend on the headset's uptime.** After a reboot, turns and idle are under 1 % (0.23–0.60 %) and a 20-turn run is 0.93 % overall; a short default run is 1.77 % because of the launch hitch. After 4 days of uptime the same build dropped 5 % idle on a cooled headset. Reboot the headset before a demo (ADR-0034). A 10-minute OVR Metrics run is still due.
- **GC during turns is at the budget, not under it everywhere.** p50 3.9 KB per frame in a development player (target ≤ 4 KB); a barge-in, with two ASR sessions in seconds, reaches 6.4 KB. Most of the rest is Mono's TLS stream allocating 16 KB per WebSocket read (ADR-0023).
- **A dark room gets a request for light, not an answer.** Below camera mean luma 25 (`JollyConfig.visionMinMeanLuma`), `capture_and_identify` and `find_object` make no SAM or Spark call and Nubi asks, in one sentence in its own voice, for a light or for the object closer to one (`too_dark`, ADR-0039). Every device room so far was this dark (mean luma 0.5–45; 0.8 in the [`4fecc17` run](perf/selftest-report.md#dark-room-vision-guard-4fecc17)), so the self-test identifies its bundled still instead; the spoken too-dark reply is tested in EditMode only, not yet heard on the headset.
- **Vision answers vary with the model's reasoning.** Tool start → answer audio was 1.6–2.1 s in the last runs (1.6–3.4 s in earlier ones); the mask appears first (0.6–1.3 s) and a preamble covers the wait.
- **The first turn after launch is a little slower.** Connections opened at launch warm TLS for the ASR and Wit; when the acknowledgement still played at release on every turn, the first one took 1.1–1.4 s vs 0.6–1.1 s later, and the first sentence's audio starts 0.2–0.3 s after it is queued vs 0.1–0.35 s later.
- **Polish TTS is not available.** Polish replies are captions-only.
- **The camera pose is gyroscope-relative.** The mapping from that pose to the OpenXR head pose is still to be confirmed by the marker test. Until then, the mask overlay and pointing may be off by a few centimetres.
- **Responses sometimes sends nothing for a request.** In the four runs of ADR-0038, 1 of 39 voice model calls got no first event within 4 s (three in the `dd14847` runs, two in a row in one `36778c1` run). A voice request silent for 2.5 s is now hedged with an identical second request (ADR-0038), and a double stall ends in the error line after 4 s instead of ~8 s. In the first runs with the hedge, the one stall hit both requests (no first event on either by 4 s): that turn got the hold line and the canned error line ("Oops, something went wrong…"), not an answer. A double stall always ends in that spoken line (EditMode, through the real agent loop and hedge); the logs now say whether any byte arrived.
- **No server-side cancel for streaming responses.** A barge-in aborts the HTTP stream on the client and books an estimate of the aborted request.
- **Push-to-talk only.** Hands-free voice barge-in and a wake word were cut for the hackathon.
- **Without environment depth, only furniture occludes.** The fallback uses Space Setup bounding boxes: no hands, no unscanned objects, and a box is larger than the furniture in it, so Nubi can be cut a little early near a couch's armrest.
- **The glasses path is unverified.** Passthrough Camera Access on Meta's glasses was not checked, so it is listed as roadmap only.

## Accessibility

- **Captions** for both you and Nubi, on by default, in three text sizes.
- **Hands-only operation.** Push-to-talk works on either hand or on the dominant hand only. Controllers also work: hold A or X ([Controls](#controls)). Sending Nubi somewhere is point and pinch briefly, or point and pull the trigger, with a ring that is light with a dark rim, so it reads on any floor. Pinches are ignored while you hover UI or poke Nubi, and while your palm faces you (the system-menu gesture).
- **Seated mode.** Nubi comes to you: `walk_to`, `follow_user` and `go_to_surface` are withheld from the model, while `move_relative` and `show_place` stay, because the character does the walking.
- **Speech rate and volume settings.** The mask highlight uses an outline *and* a fill, so it doesn't rely on colour alone.

## Localization

Unity Localization 1.5.13, with locales `en` (default) and `pl`.
- The `UI`, `Lines`, `Personas` and `Crisis` string tables ship in English and Polish.
- ASR takes a language bias per locale.
- Wit has no Polish voice for this app (only `en_US`, `en_CA` and `en_GB` were listed), so Polish replies are captions-only.
- Baked offline lines are English today.
- Tool schemas stay in English because only the model reads them.
- The persona instructions get a language directive.

## Roadmap

These items are **not built.** They are where the architecture is designed to go.

- **Meta glasses.** The same brain (ASR → Spark tools → SAM → TTS) with a camera that already sees what you see. This is the natural form factor for low-vision help.
- **Android XR and Pico through OpenXR.** `Jolly.XR` is already free of Meta types, so a new platform needs a frame-source adapter beside `Jolly.XR.MetaQuest`. Note that the Meta SDK licence restricts its components to Meta hardware.
- **Offline and private models.** On-device STT and TTS, or a LAN host for open-weight Muse Glimmer (Apache-2.0) behind `IChatModel`, so nothing leaves the home.
- **A therapist-grade private mode.** The Listener persona (id `psychologist`) keeps no memory, uses no camera tools and gives a safety notice. A real counselling mode only makes sense once it runs on private models with professional review, so it is not demoed, and the app never calls it a psychologist.
- **Hands-free conversation.** Playback-gated VAD for voice barge-in, and a wake word.

### Why Quest today, glasses tomorrow

The brain does not depend on the headset: the asmdefs of `Jolly.Contracts`, `Jolly.Core`, `Jolly.Net`, `Jolly.Providers.MetaApi` and `Jolly.Providers.Wit` reference only each other and UniTask, no XR package, so the agent loop, tools, memory, ASR, Spark, SAM and Wit would carry over unchanged. A glasses build would need new adapters for the device edges declared in `Jolly.Contracts`: an `ICameraFrameSource` for the glasses camera (frames with intrinsics and a camera pose), an `IAudioCapture` and an `ISpeechOutput` for its microphones and speakers, and an `IUserPoseProvider` for where the wearer is. None of these adapters exists today. Quest 3 is the prototype because what makes Nubi mixed reality needs a world-locked display and room understanding: a body that walks a NavMesh baked from the real floor, the SAM mask drawn over the real object in passthrough, a hand ray as the pointing prompt, and depth occlusion.
