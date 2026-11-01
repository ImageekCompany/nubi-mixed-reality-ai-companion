# Device self-test report

Latest: 2026-11-01, 08:20–08:47 UTC, the performance and latency pass on `fix/perf-latency` (ADR-0019): two "before" runs of `main` plus the new diagnostics, four "after" runs, release IL2CPP. Earlier: 06:38–06:50 UTC from `fix/integration-review`, 06:00–06:14 UTC from `feat/wp9-integration`.
Device: Quest 3 `2G0YC1ZF7P090N`, Horizon OS v209. Build: `tools/ci/build.sh` (IL2CPP ARM64 release, 58 MB APK).
Nobody wore the headset. The room was dim (camera mean luma 23–45 of 255) and had no Space Setup. Command: `tools/device/selftest.sh` (headless: `prox_close`, `guardian_pause 1`, 16 MiB logcat buffer; all restored afterwards).
Raw outputs (`selftest_results.json`, `logcat.txt`, `vrapi.txt`) are under `artifacts/selftest/`, which is git-ignored.

## Performance and latency pass (fix/perf-latency)

Phases: **launch** is app start to 2 s after the first scenario starts (the launch hitch lands there), **turns** is the rest up to `perf_idle`, **idle** is `perf_idle`. A VrApi line covers the second before its timestamp. "Spikes" are main-thread frames over 28 ms (`evt=frame.spike`).

| Metric | Target | Before (2 runs) | After (4 runs) |
|---|---|---|---|
| Stale frames, turns | < 1 % | 1.28 %, 1.06 % | 0.99 %, 0.78 %, 0.55 %, 0.94 % |
| Main-thread spikes in turns | — | 9, 6 | 4, 2, 3, 2 |
| Stale frames, launch | — | 48, 78 frames | 76, 78, 33, 75 frames |
| Longest launch frame | — | 336, 319 ms | 308–319 ms |
| Stale frames, idle | < 1 % | 0.22 %, 0.18 % | 0.22 %, 0.18 %, 0.04 %, 0.22 % |
| First ASR ack of a launch | — | 2,047, 1,543 ms (four dev runs: 1,493–2,253) | 1,427, 1,732, 719, 1,122 ms |
| Release → first reply audio | p50 ≤ 2,000 ms | p50 1,646, max 2,137 (n=8) | p50 1,443, max 2,872 (n=16) |
| ASR final → first LLM text | p50 ≤ 1,300 ms | p50 1,190 (n=8) | p50 840 (n=16) |
| Agent calls: cached input tokens | — | 0 on every call | p50 2,033 of ~2,500 |
| Agent call cost (> 2k input) | — | mean $0.00313 | mean $0.00203 |
| Vision header after the tool start | — | 1,377, 2,456 ms | 1,998, 1,734, 1,276, 3,069 ms |
| Tool start → answer audio | p50 ≤ 2,500 ms | 1,695, 2,763 ms | 2,443, 2,042, 1,555, 3,389 ms (p50 2,243) |
| Tool start → mask shown | p50 ≤ 2,000 ms | 975, 1,042 ms | 1,127, 722, 988, 1,043 ms |

A verification run of the final branch code (08:51 UTC; the four "after" runs predate dropping the one-AR-manager-per-frame start and the vision no-tools line): turns **0.98 %** stale with 5 spikes, first ASR ack 1,940 ms, vision header 1,637 ms (parsed), tool start → answer audio **2,194 ms**, mask 863 ms, release → first audio 1,430–1,802 ms. The first-ack gain is the noisiest result: p50 over the five runs with the warm-up is 1,427 ms against ~1.8 s over the six runs without it.

What was found and done (ADR-0019):

- **Stale frames in turns were first uses.** The first caption, badge, mask and pose each built their Vulkan pipelines at first draw, and the first PTT release read the Camera2 lens pose over JNI (20 ms inside a 53 ms frame). A `GraphicsStateCollection` recorded by a development-player self-test is warmed at boot, and one discarded camera capture at launch absorbs the lens pose, the CPU-image conversion and the JPEG encoder. Turns are now under 1 % in all four runs; the remaining stale frames are mostly single frames with no main-thread spike.
- **The launch hitch is the passthrough camera start.** Enabling `ARCameraManager` starts Meta's camera synchronously, and the `AndroidCameraManager` log lines fall inside a ~320 ms frame. Starting the AR managers one per frame did not change it and was dropped. It happens before Nubi has said anything.
- **The cold first turn** paid a one-time TLS and WebSocket setup: the first ASR ack of a launch took 1.5–2.3 s against 0.7–1.0 s later. A connection opened and dropped before the handshake at launch (no session, nothing billed) brings the first ack to 0.7–1.7 s.
- **Vision latency is reasoning time.** On the device the header arrives 1.3–3.1 s after the tool start with 200–500 reasoning tokens at `minimal`, varying run to run. Probes from the dev Mac (`meta-model-api.md` §3, 9 paid calls): `muse-spark-1.2` was slower (2.4–2.7 s), a "reply at once" prompt made the model reason more (2.29 s vs 1.81 s), and image size only moves the 300–400 image tokens of prefill, so 640 px and quality 80 stay. SAM and Spark already start together; the mask is shown 0.7–1.1 s after the tool start. The 2.5 s budget is met in 3 of 4 runs. The remaining lever is starting the vision call at the ASR final for turns that sound like a vision question, before the model calls the tool (ADR-0019 Alternatives).
- **Prompt caching.** Tool-bearing Responses requests were never cached without a `prompt_cache_key`; every request now sends one derived from the static prefix. Cost per agent call fell by a third; the latency effect is within the noise of these runs.
- **Bugs found on the way:** every memory summary call ended in reasoning at the 300-token cap and replaced the summary with nothing (now 1,024 tokens, and an unusable result changes nothing); one vision reply ran into the 600-token cap; another was 40 s of reasoning about the missing `capture_and_identify` tool, because persona prompts name tools (the vision prompt now says it has none; verified live).
- **GC during turns is over budget.** Development players measured 11–42 KB per frame during turns (DoD: ≤ 4 KB) and ~200 B per frame idle. Not addressed in this pass.

Spend: 11 self-test runs ($0.243 by the app meter) and 13 probe calls (≈ $0.04); `api-spend.md` has the rows.

## What runs for real

The self-test (`Runtime/App/SelfTest`, ADR-0018) replaces only what a person provides and runs everything else in the shipped app:

| Replaced | By | Real path exercised |
|---|---|---|
| A voice | 24 kHz Wit-synthesised clips fed into `UnityMicrophoneCapture` through `ClipMicrophone` (same ring, resampler, chunker) | Muse realtime ASR over `ClientWebSocket` → aliases/guards → Spark Responses (SSE, tools) → Wit TTS → `StreamingSpeechOutput` (`OnAudioFilterRead`) + uLipSync → captions |
| The pinch | `ScriptedPushToTalk` | `ConversationController`, barge-in, watchdogs |
| The Wi-Fi switch | a reachability override | `ConnectivityMonitor`, health probe, `OfflineResponder` with the baked clips |
| A lit room | the CC0 mug still when the live frame's mean luma is under 25/255 (marked DEGRADED) | passthrough capture, JPEG encode, SAM 3.1 (point prompt), Spark vision, mask overlay, scene memory |

## Integration review run (06:48 UTC, `fix/integration-review`)

| Scenario | Status | Numbers |
|---|---|---|
| `offline_greeting` | PASS | network forced off → Offline in < 1 s; press → caption and clip audio **14 ms**; back Online after the switch |
| `voice_greeting` ("Hi Nubi, who are you?") | PASS | transcript exact; release → ASR final 383 ms, final → first LLM text 631 ms, release → **first reply audio 1,500 ms** (first turn after launch) |
| `voice_come_here` | DEGRADED (env) | transcript exact; `come_here` called; release → first audio **1,471 ms**; no NavMesh (no Space Setup), and the reply is now consistent: "Let me try. I can't reach you just yet." |
| `vision_capture_and_identify` | PASS | **live** passthrough frame (luma 26.1); SAM point prompt → 1 object in 813 ms; Spark named the headset on the desk and explained it; release → first audio **1,192 ms**, which is the preamble ("Let me check."), not the answer |
| `barge_in` | PASS | press while speaking → queue flushed in **6 ms**, rendered output silent in **28 ms** (target ≤ 150 ms) |
| `perf_idle` (30 s, Nubi idle) | PASS | **72.0 fps** average, p95/p99 frame **13.9 ms**, **12** draw calls and **11** SetPass calls at most, **27,178** triangles, 0 GC collections, mono heap 12.6 MB, system memory 531 MB |

Overall: **DEGRADED**, only because of the room (no Space Setup). Meta spend of this run: **$0.0260** (app meter).

### All three runs on this branch

| Run (UTC) | voice_greeting | voice_come_here | vision (preamble) | barge-in silence | Spend |
|---|---|---|---|---|---|
| 06:38 | 1,847 ms | 1,569 ms | 1,849 ms (still, luma 23.1) | 54 ms | $0.0232 |
| 06:43 | 1,971 ms | 1,944 ms | 1,081 ms (live, see bug 5) | 54 ms | $0.0243 |
| 06:48 | 1,500 ms | 1,471 ms | 1,192 ms (live) | 28 ms | $0.0260 |

Release → first reply audio over the nine online turns: p50 **1,569 ms**, max 1,971 ms; all under the 2.0 s target, including the first turn of each launch. This is 9 turns, not the 20 consecutive turns DoD §5 asks for. The first ASR session of a launch still acknowledges late (`asr.ack` 1.8–2.3 s vs 0.65–0.99 s warm), but the audio captured meanwhile is sent at once after the ack, so it costs little for an utterance of 1.5 s or more.

### Compositor (VrApi) stale frames, 06:48 run

| Phase | Stale / frames |
|---|---|
| Launch and first scenario start (first ~6 s) | 81 / 390, including one 0.5 s hitch (`FPS=35/72 Stale=28`) when the health probe and the first scenario start |
| Conversation turns (offline, voice, vision, barge-in) | 36 / 3,118 (**1.15 %**) |
| Idle (`perf_idle`) | 4 / 2,253 (0.18 %) |

The DoD target is < 1 % over 10 minutes of conversation. Turns are just over it, and the launch hitch is not explained yet (speculation: first-use shader or asset loads, or a blocking call on the first probe). `selftest.sh` now prints the whole-run share; a development-build Profiler capture of one turn is the next step (`docs/perf/README.md` §4).

## Bugs the self-test found (fixed, ADR-0018)

1. **The app could never leave Offline.** `MetaHealthProbe` requested `/v1/v1/models` (404). Runs 1–2 failed every online scenario; after the fix the probe reads Online within a second.
2. **Phantom pinches.** With nobody wearing the headset, the pinch detector produced press/release pairs that barged into scripted turns. The self-test no longer forwards the pinch. Whether this can happen to a wearer is open (see below).
3. **Tool preamble glued to the next round** (found by the E2E test before the device run): "On my way!Here I am." is now spoken as two sentences, the first before the tool runs.
4. **The preamble promised what the tool then denied** ("I'll come right over to you. I can't reach you just yet."). The prompt rule now asks for a neutral phrase and forbids stating the outcome first (fixed on `fix/integration-review`).
5. **Silent vision turn.** With a dim live frame, the vision stream completed with nothing to say, so Nubi said "Let me check." and stopped. An empty answer now speaks the hold-closer line.
6. **Unity warned on every canned clip** ("Data longer than the AudioClip"), and every Info log captured a managed stack trace in the release player. Both fixed; the 06:38–06:48 logcats have neither.

## Not covered here (needs a person or a lit, scanned room)

- Live passthrough content, mask placement against real geometry and the R1 marker test (`device-spike.md` §5).
- Walking: the scene has no Space Setup, so no floor NavMesh; `come_here` was checked only for its graceful answer.
- Hand-tracking pinch, poke and gaze triggers, and whether the phantom pinch appears with a wearer.
- GC allocation per frame: "GC Allocated In Frame" is recorded only in development players; the release run reports collections (0 in 30 s).
- `capture_and_identify` start → mask visible and → answer audio (DoD §5): no event marks either on the device yet. The vision row above is the preamble.
- DoD §4 rows that need OVR Metrics or the Profiler (CPU main thread p95, `app_rss_MB`, 10-minute stale frames, thermal, battery), and §5's 20 consecutive turns. See the coverage table below.

## DoD §4–§5 coverage

DoD §4 asks for deviations to be recorded in `docs/perf/report.md`; until the OVR Metrics capture and a development-build Profiler session exist, this table is that record. **Unmeasured** means no number exists anywhere yet.

| DoD row | Target | Measured (release player, runs above) | Status |
|---|---|---|---|
| Frame rate | ≥ 72 fps | 72.0 fps idle, p95 13.9 ms | Met at idle |
| Stale frames | < 1 % over 10 min | turns 0.55–0.99 % (4 runs), idle ≤ 0.22 %, a ~320 ms frame at launch (camera start) | Met during turns on ~1 min samples; 10 min unmeasured |
| CPU main thread | p95 ≤ 11 ms | — | **Unmeasured** (Profiler) |
| Draw calls / SetPass | ≤ 200 / ≤ 60 | 12 / 11 max, idle | Met at idle; turns unmeasured |
| Triangles | ≤ 300k; Nubi ≤ 30k | 27,178 max; Nubi 25,248 | Met |
| GC alloc | 0 B/frame idle; ≤ 4 KB/frame in a turn | release: 0 collections in 30 s; development player: ~200 B/frame idle, 11–42 KB/frame in turns | **Not met during turns**; idle met in the release player and in `NoGcAllocInSteadyStateIdle` |
| Memory | `app_rss_MB` ≤ 1,500 MB | System Used Memory 531 MB | `app_rss_MB` **unmeasured** |
| Thermal, battery | 30 min / ≤ 20 % | — | **Unmeasured** |
| Release → ASR final | p50 ≤ 300 ms | p50 208 ms, max 681 ms (n=16, 4 runs of `fix/perf-latency`) | Met on n=16 |
| ASR final → first text | p50 ≤ 1,300 ms | p50 840 ms, max 2,250 ms (n=16) | Met on n=16 |
| **Release → first answer audio** | **p50 ≤ 2,000 ms** | p50 1,443 ms, max 2,872 ms (n=16) | Met on n=16, not 20 |
| Pinch → silence | p50 ≤ 150 ms | 28–54 ms | Met |
| Tool start → mask visible | p50 ≤ 2,000 ms | 722–1,127 ms (`evt=vision.timing`, 4 runs) | Met |
| Tool start → answer audio | p50 ≤ 2,500 ms | 1,555–3,389 ms, p50 2,243 (4 runs, live dim frames) | Met at p50; 1 of 4 over (model reasoning time) |
| Network loss → offline line | ≤ 2,000 ms | Offline in < 1 s, caption 14–27 ms after the press | Met |

## How to run

```bash
tools/ci/build.sh                                  # or pass --build to selftest.sh
NUBI_TMPDIR=<short temp dir> tools/device/selftest.sh   # ~2 min, ~$0.02-0.03; exit 1 on any FAIL
```
