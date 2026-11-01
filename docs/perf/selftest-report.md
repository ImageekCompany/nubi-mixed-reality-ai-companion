# Device self-test report

Date: 2026-11-01, 06:00–06:14 UTC. Device: Quest 3 `2G0YC1ZF7P090N`, Horizon OS v209. Build: `tools/ci/build.sh` (IL2CPP ARM64 release, 58 MB APK) from `feat/wp9-integration`.
Nobody wore the headset. The room was dark and had no Space Setup. Command: `tools/device/selftest.sh` (headless: `prox_close`, `guardian_pause 1`, 16 MiB logcat buffer; all restored afterwards, `guardian_pause` read back `0`).
Raw outputs (`selftest_results.json`, logcat) are under `artifacts/selftest/`, which is git-ignored.

## What runs for real

The self-test (`Runtime/App/SelfTest`, ADR-0018) replaces only what a person provides and runs everything else in the shipped app:

| Replaced | By | Real path exercised |
|---|---|---|
| A voice | 24 kHz `say` clips fed into `UnityMicrophoneCapture` through `ClipMicrophone` (same ring, resampler, chunker) | Muse realtime ASR over `ClientWebSocket` → aliases/guards → Spark Responses (SSE, tools) → Wit TTS → `StreamingSpeechOutput` (`OnAudioFilterRead`) + uLipSync → captions |
| The pinch | `ScriptedPushToTalk` | `ConversationController`, barge-in, watchdogs |
| The Wi-Fi switch | a reachability override | `ConnectivityMonitor`, health probe, `OfflineResponder` with the baked clips |
| A lit room | the CC0 mug still when the live frame's mean luma is under 25/255 (marked DEGRADED) | passthrough capture, JPEG encode, SAM 3.1 (point prompt), Spark vision, mask overlay, scene memory |

## Final run (06:13 UTC)

| Scenario | Status | Numbers |
|---|---|---|
| `offline_greeting` | PASS | network forced off → Offline in < 1 s; press → caption **15 ms**, clip audio 15 ms; back Online after the switch |
| `voice_greeting` ("Hi Nubi, who are you?") | PASS | transcript "Hi, Nubi. Nubi. Who are you?" (similarity 0.91); release → ASR final **610 ms**, final → first LLM text 1,195 ms, release → **first reply audio 2,221 ms** (first, cold session) |
| `voice_come_here` | DEGRADED (env) | transcript exact; `come_here` called; release → first audio **1,681 ms**; no NavMesh (no Space Setup), so the tool answered unreachable and Nubi said "I can't reach you just yet." |
| `vision_capture_and_identify` | DEGRADED (env) | live frame mean luma 13.4 → bundled still; `capture_and_identify` with SAM + streamed vision answer ("a little enamel cup … hold it by the handle"); release → first audio **1,676 ms** |
| `barge_in` | PASS | press while speaking → queue flushed in **10 ms**, rendered output silent in **27 ms** (target ≤ 150 ms); new Listening, empty final → Idle |
| `perf_idle` (30 s, Nubi idle) | PASS | **72.0 fps** average, p95/p99 frame **13.9 ms**, 0 GC collections, mono heap 13.1 MB, system memory 533 MB |

Overall: **DEGRADED**: every failure condition passed; the two DEGRADED results are the environment (dark room, no Space Setup). Meta spend of this run: **$0.025** (app meter).

Release → first reply audio over the three online turns of the previous run (06:00 UTC): 1,942 / 1,986 / 1,760 ms; this run: 2,221 / 1,681 / 1,676 ms. The only turn over the 2.0 s budget is the first of each launch, when the ASR TLS connection is cold (ack 2.2 s after connect in an earlier run vs ~0.7 s warm).

## Bugs the self-test found (fixed, ADR-0018)

1. **The app could never leave Offline.** `MetaHealthProbe` requested `/v1/v1/models` (404). Runs 1–2 failed every online scenario; after the fix the probe reads Online within a second.
2. **Phantom pinches.** With nobody wearing the headset, the pinch detector produced press/release pairs that barged into scripted turns. The self-test no longer forwards the pinch. Whether this can happen to a wearer is open (see below).
3. **Tool preamble glued to the next round** (found by the E2E test before the device run): "On my way!Here I am." is now spoken as two sentences, the first before the tool runs.

## Not covered here (needs a person or a lit, scanned room)

- Live passthrough content, mask placement against real geometry and the R1 marker test (`device-spike.md` §5).
- Walking: the scene has no Space Setup, so no floor NavMesh; `come_here` was checked only for its graceful answer.
- Hand-tracking pinch, poke and gaze triggers, and whether the phantom pinch appears with a wearer.
- GC allocation per frame: "GC Allocated In Frame" is recorded only in development players; the release run reports collections (0 in 30 s).

## How to run

```bash
tools/ci/build.sh                                  # or pass --build to selftest.sh
NUBI_TMPDIR=<short temp dir> tools/device/selftest.sh   # ~2 min, ~$0.02-0.03; exit 1 on any FAIL
```
