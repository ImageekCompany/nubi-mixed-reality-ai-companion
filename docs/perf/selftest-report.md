# Device self-test report

Latest: 2026-11-01, 06:38–06:50 UTC, three runs from `fix/integration-review` (after the integration review fixes). Earlier: 06:00–06:14 UTC from `feat/wp9-integration`.
Device: Quest 3 `2G0YC1ZF7P090N`, Horizon OS v209. Build: `tools/ci/build.sh` (IL2CPP ARM64 release, 58 MB APK).
Nobody wore the headset. The room was dim (camera mean luma 23–26 of 255) and had no Space Setup. Command: `tools/device/selftest.sh` (headless: `prox_close`, `guardian_pause 1`, 16 MiB logcat buffer; all restored afterwards).
Raw outputs (`selftest_results.json`, `logcat.txt`, `vrapi.txt`) are under `artifacts/selftest/`, which is git-ignored.

## What runs for real

The self-test (`Runtime/App/SelfTest`, ADR-0018) replaces only what a person provides and runs everything else in the shipped app:

| Replaced | By | Real path exercised |
|---|---|---|
| A voice | 24 kHz Wit-synthesised clips fed into `UnityMicrophoneCapture` through `ClipMicrophone` (same ring, resampler, chunker) | Muse realtime ASR over `ClientWebSocket` → aliases/guards → Spark Responses (SSE, tools) → Wit TTS → `StreamingSpeechOutput` (`OnAudioFilterRead`) + uLipSync → captions |
| The pinch | `ScriptedPushToTalk` | `ConversationController`, barge-in, watchdogs |
| The Wi-Fi switch | a reachability override | `ConnectivityMonitor`, health probe, `OfflineResponder` with the baked clips |
| A lit room | the CC0 mug still when the live frame's mean luma is under 25/255 (marked DEGRADED) | passthrough capture, JPEG encode, SAM 3.1 (point prompt), Spark vision, mask overlay, scene memory |

## Final run (06:48 UTC, `fix/integration-review`)

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
| Stale frames | < 1 % over 10 min | turns 1.15 %, idle 0.18 %, launch hitch | **Not met during turns** (~2 min sample) |
| CPU main thread | p95 ≤ 11 ms | — | **Unmeasured** (Profiler) |
| Draw calls / SetPass | ≤ 200 / ≤ 60 | 12 / 11 max, idle | Met at idle; turns unmeasured |
| Triangles | ≤ 300k; Nubi ≤ 30k | 27,178 max; Nubi 25,248 | Met |
| GC alloc | 0 B/frame idle; ≤ 4 KB/frame in a turn | 0 collections in 30 s; `NoGcAllocInSteadyStateIdle` passes | Idle met; per-turn **unmeasured** |
| Memory | `app_rss_MB` ≤ 1,500 MB | System Used Memory 531 MB | `app_rss_MB` **unmeasured** |
| Thermal, battery | 30 min / ≤ 20 % | — | **Unmeasured** |
| Release → ASR final | p50 ≤ 300 ms | 207–464 ms (n=9) | Met on n=9 |
| ASR final → first text | p50 ≤ 1,300 ms | 388–1,367 ms (n=9) | Met on n=9 |
| **Release → first answer audio** | **p50 ≤ 2,000 ms** | p50 1,569 ms, max 1,971 ms (n=9) | Met on n=9, not 20 |
| Pinch → silence | p50 ≤ 150 ms | 28–54 ms | Met |
| Tool start → mask visible | p50 ≤ 2,000 ms | SAM done 494–813 ms after the call | Mask time **unmeasured** |
| Tool start → answer audio | p50 ≤ 2,500 ms | vision header 2.8–3.1 s after the call (dim live frame) | **Unmeasured**, likely over |
| Network loss → offline line | ≤ 2,000 ms | Offline in < 1 s, caption 14–27 ms after the press | Met |

## How to run

```bash
tools/ci/build.sh                                  # or pass --build to selftest.sh
NUBI_TMPDIR=<short temp dir> tools/device/selftest.sh   # ~2 min, ~$0.02-0.03; exit 1 on any FAIL
```
