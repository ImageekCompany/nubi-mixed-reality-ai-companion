# Device self-test report

Latest: 2026-11-01, 15:22–15:28 UTC, three default release runs of `36778c1` with the local come-here guard and `llm.reply` (ADR-0036; section below). Before that: 14:47–14:53 UTC, three default release runs verifying `main` `f311081` after round 1 (section below). Before that: 14:05–14:37 UTC, stale-frame cause and a 20-turn latency sample on `751acba` (cooled headset, before and after a headset reboot; section below). Before that: 13:03 UTC, one release run of `6f0383c` with the `highlight_part` / `find_object` scenarios and the hand debounce (ADR-0033; section below). Before that: 11:43–11:51 UTC, final verification of `main` `4d228c8` after the occlusion, room, character, Jolly and device-fix merges: three release runs of the default build and one of `--character jolly` (section below). Before that: 11:26–11:38 UTC, six release runs for the device-finding fixes (`3f35f7f`, `6cd06e6`, `4e298d8`; section below). Before that: 09:39–09:45 UTC, three release runs of `main` `dd14847` after the turn-gc and coverage-gate merges (section below). Before that: 08:37–09:27 UTC, the turn-GC and cold-first-turn pass on `fix/turn-gc` (ADR-0023): three "before" runs of `main`, three "after" runs, development IL2CPP (the only player that reports GC per frame), four allocation-profile runs and one release verification run. Before that: 08:20–08:47 UTC, the performance and latency pass on `fix/perf-latency` (ADR-0019), release IL2CPP. Earlier: 06:38–06:50 UTC from `fix/integration-review`, 06:00–06:14 UTC from `feat/wp9-integration`.
Device: Quest 3 `2G0YC1ZF7P090N`, Horizon OS v209. Build: `tools/ci/build.sh` (IL2CPP ARM64 release, 58 MB APK).
Nobody wore the headset. The room was dim (camera mean luma 23–45 of 255) and had no Space Setup. Command: `tools/device/selftest.sh` (headless: `prox_close`, `guardian_pause 1`, 16 MiB logcat buffer; all restored afterwards).
Raw outputs (`selftest_results.json`, `logcat.txt`, `vrapi.txt`) are under `artifacts/selftest/`, which is git-ignored.

## Local come-here guard and reply log (36778c1)

`main` at `36778c1` (ADR-0036: `ComeGuard`, `evt=llm.reply` in self-test runs, `voice_come_here` now says "Hi Nubi, come here."). `tools/ci/test.sh` (EditMode 1081/1081, PlayMode 379/379 + 1 explicit eval skipped, pure logic 92.7 % ≥ 91.6 %, 0 `warning CS`), `tools/ci/build.sh` (IL2CPP ARM64 release, 60 MB, `QuestBuild` 0 errors / 5 warnings, SHA-256 `41cc3cc076c5…`). Three default self-tests back to back (headset uptime 1 h 14 min, not rebooted; battery 32 °C), same dim room (vision used the bundled still), no Space Setup (`navmesh=fallback`). `guardian_pause` read back 0 after each run.

| | Run 1 (15:22) | Run 2 (15:24) | Run 3 (15:26) | Last report (`f311081`) |
|---|---|---|---|---|
| `voice_come_here` | DEGRADED (first audio 2,235 ms) | **FAIL** (model timed out, no reply audio) | DEGRADED (first audio 2,138 ms) | PASS / FAIL / FAIL |
| Walk started by (`come_source`), walk outcome | guard, arrived | guard, arrived | guard, arrived | model 1 of 3 |
| Come-here reply (`llm.reply`) | "I'm on my way over to you now." (no tool) | — (request timed out twice) | "I'm on my way to you now." (no tool) | not logged |
| Greeting tools (stayed put) | `play_gesture` (yes) | `play_gesture` (yes) | none (yes) | yes in 3 of 3 |
| `find_object` routing | `find_object`, then `look_at` | `find_object` | `find_object`, then `look_at` | 2 of 3 `find_object` |
| Release → first audio, the 5 voice scenarios (ms) | 4,707 / 2,235 / 2,183 / 2,987 / 1,541 | 3,540 / — / — / 2,613 / 2,902 | 2,396 / 2,138 / 1,735 / 2,083 / 2,100 | — |
| Release → first audio p50 | 2,235 | 2,902 (n=3) | 2,100 | 1,903–1,929 |
| Stale frames, whole run | 2.06 % | 2.62 % | 2.68 % | 1.22–1.63 % |
| Worst launch frame (`FrameSpikeLog`) | 286.6 ms | 278.7 ms | 279.4 ms | 274–286 ms |
| Barge-in silence | 41 ms | 39 ms | 48 ms | 29–55 ms |
| Overall, spend | **FAIL** (greeting 4,707 ms), $0.0277 | **FAIL**, $0.0261 | DEGRADED, $0.0247 | |

Findings:

- **The guard started the walk in 3 of 3 runs and Nubi arrived each time**, about 250 ms after release (`evt=guard.come status=walking` at the ASR final), before any model round. The model followed the note in both runs where it answered: a short line, no second `come_here` call (`come_source=guard`).
- **`voice_come_here` still did not PASS**: runs 1 and 3 degraded on first audio (2,235 / 2,138 ms against 2.0 s; request → first event 1.35–1.39 s, model time), and in run 2 the Responses request timed out twice in a row (`evt=llm.failed kind=Timeout`, hold lines played) for this turn and the next one, so there was no spoken reply although Nubi walked. The acceptance target (PASS 3/3) is not met; the movement target is.
- **`llm.reply` explains the other misses.** Run 2's `vision_highlight_part` FAIL: the model called `capture_and_identify` again ("Let me check.") instead of `highlight_part`. `find_object` routed correctly in all three runs; twice the model then added `look_at` in a second round, which costs a round (tool start → answer 3.0–4.9 s).
- **Stale frames 2.1–2.7 %**, above the last report, on a headset not rebooted since the morning sessions; ADR-0034 found this follows uptime. Idle fps, draw calls and memory are unchanged (72.0 fps, p95 13.9 ms, 14 draw calls, 556–559 MB).

Spend: 3 runs ($0.0277 + $0.0261 + $0.0247); `api-spend.md` has the row. The new self-test clip came from Wit `/synthesize` (free).

## Round 1 verification of main (f311081)

`main` at `f311081` (no code change since `751acba`; the commits after it are docs). `tools/ci/test.sh` (EditMode 1056/1056, PlayMode 360/360, pure logic 92.7 % ≥ 91.6 %, 0 `warning CS`), `tools/ci/build.sh` (IL2CPP ARM64 release, 59 MB, `QuestBuild` 0 errors / 2 warnings, SHA-256 `155eff54bd37…`). Three default self-tests back to back on the same headset (uptime 39 min at the start, battery 35 °C), same dim room (mean luma 0.7, vision used the bundled still), no Space Setup. `guardian_pause` read back 0 after each run.

| | Run 1 (14:47) | Run 2 (14:49) | Run 3 (14:51) | Last report (B / C, `751acba`) |
|---|---|---|---|---|
| Release → first audio, the 5 voice scenarios (ms) | 3,277 / 2,376 / 1,928 / 1,166 / 1,709 | 3,292 / 1,513 / 3,472 / 1,112 / 1,903 | 2,956 / 2,179 / 1,792 / 960 / 1,929 | — |
| Release → first audio p50 | 1,928 | 1,903 | 1,929 | C: 1,804 (n=20) |
| Greeting release → first audio | 3,277 | 3,292 | 2,956 | B: 4,067; C greeting p50 2,362 |
| Worst launch frame (`FrameSpikeLog`) | 285.9 ms | 279.6 ms | 274.1 ms | 279–290 ms |
| Idle fps / p95 frame | 72.0 / 13.9 ms | 72.0 / 13.9 ms | 72.0 / 13.9 ms | 72.0 / 13.9 ms |
| Stale frames, launch / turns / idle | 53 / 0.71 % / 0.22 % | 87 / 0.62 % / 0.13 % | 82 / 0.70 % / 0.18 % | B: 97 / 0.60 % / 0.32 % |
| Stale frames, whole run | 1.22 % | 1.63 % | 1.62 % | B: 1.77 % |
| Idle draw / SetPass / triangles / system memory | 14 / 12 / 27.3k / 559 MB | 14 / 12 / 27.3k / 557 MB | 14 / 12 / 27.2k / 559 MB | B: 13 / 12 / 27.1k / 558 MB |
| Barge-in silence | 29 ms | 55 ms | 40 ms | 28–54 ms |
| Overall, spend | DEGRADED, $0.0259 | **FAIL**, $0.0248 | **FAIL**, $0.0189 | |

Findings:

- **No frame, stale-frame or memory regression.** Idle, launch and turn numbers are within the spread of the last report.
- **`voice_come_here` FAILed in runs 2 and 3: the model answered in words without calling `come_here`** (ASR similarity 1.0, `finish=Stop`, one model round; run 1 called it). The model never skipped this tool in earlier reports (7 of 7 in the 20-turn run on the same code), and nothing in the app changed since, so this is model-side variance, not a round 1 regression; it was not fixed here. The app does not log reply text, so what the model said is not known. Run 3 also called `look_at` instead of `find_object` (DEGRADED) and `play_gesture` on the greeting.
- **The greeting is the slow turn**, 3.0–3.3 s in all three runs; the other turns took 1.0–2.4 s except one vision turn (3,472 ms). The time is model time (request → first text 1.1–2.8 s), as in every earlier report; release → ASR final was 208–361 ms except one 1,302 ms greeting (run 3).

Spend: 3 runs ($0.0259 + $0.0248 + $0.0189); `api-spend.md` has the row.

## Stale frames are device state, not the build or heat; 20-turn latency (751acba)

`main` at `751acba` (`selftest.sh --turns N`, ADR-0034). `tools/ci/test.sh` (EditMode 1056/1056, PlayMode 360/360, pure logic 92.7 %, 0 `warning CS`), `tools/device/build.sh` (IL2CPP ARM64 release, 59 MB, `QuestBuild` 0 errors / 5 warnings, SHA-256 `1c07e305f44e…`). OVR Metrics Tool (`com.oculus.ovrmonitormetricsservice`) CSV capture was on for every run. Temperatures: `dumpsys battery` (live); `dumpsys thermalservice` returned the same cached sensor values while the headset slept, so its readings are not used here, and its thermal status was 0 (none) throughout. Same dim room (mean luma 0.5–15, vision used the bundled still), no Space Setup.

Sequence: headset idle 20+ min, default self-test (A, 14:05); `adb reboot` after 4 days 14 h of uptime; idle 20 min; default self-test (B, 14:28); `--turns 20` (C, 14:30); then six launch-only runs (below). The default build was reinstalled at the end (installed APK SHA-256 matches) and `guardian_pause` read back 0.

| | A: cooled, 4 d uptime | B: after reboot | C: `--turns 20`, after reboot |
|---|---|---|---|
| Battery at start → end | 32 → 33 °C | 31 → 33 °C | 34 → 35 °C |
| Stale frames, launch (to first scenario + 2 s) | 109 | 97 | 85 |
| Stale frames, turns | **5.21 %** (253 / 4,858) | **0.60 %** (30 / 5,006) | **0.49 %** (73 / 14,887) |
| Stale frames, idle (`perf_idle`, 30 s) | **5.01 %** (109 / 2,177), 2–5 every second | **0.32 %** (7 / 2,175) | **0.23 %** (5 / 2,178) |
| Stale frames, whole run | 6.24 % | 1.77 % | **0.93 %** |
| App GPU time p50 (OVR Metrics), GPU utilisation | **5.44 ms**, 47 % | **3.00 ms**, 29 % | 3.22 ms, 30 % |
| CPU / GPU level in idle | 2 / 2 | 2 / 2 | 2 / 2 |
| App main-thread frames > 28 ms after launch | 2 (worst 36.5 ms) | 6 (worst 38.7 ms) | 5 (worst 35.8 ms) |
| App memory: `app_rss_MB` peak (OVR Metrics) / `dumpsys meminfo` | 630 MB / — | 642 MB / RSS 644 MB, PSS 463 MB | 643 MB / RSS 643 MB, PSS 463 MB |
| Idle draw / SetPass / triangles / system memory | 15 / 13 / 28.3k / 546 MB | 13 / 12 / 27.1k / 558 MB | 17 / 15 / 28.5k / 562 MB |
| Overall, spend | DEGRADED, $0.0241 | FAIL (greeting 4,067 ms), $0.0276 | PASS, $0.0845 |

Findings:

- **The idle stale-frame rate is device state that a reboot clears, not the build and not heat.** The same APK on a headset cooled for 20+ minutes (battery 32 °C) dropped 2–5 frames every second of idle (5.0 %), worse than the warm runs of the last report (0.9–2.2 %). After `adb reboot`, with the same APK and the same battery temperature, idle was 0.23–0.32 % and turns 0.49–0.60 %. OVR Metrics shows why it is not the app's CPU side: stale frames were already 4 per second during the Unity splash screen, before any app code ran, and the app's GPU time for the same idle scene at the same GPU level fell from 5.4 to 3.0 ms (utilisation 47 → 29 %) across the reboot. The steady rise over the day (idle 0.04 % in the morning, 5 % at 4 days 14 h uptime) follows uptime, not the builds. speculation: something on the GPU outside the app (the headset had 610 MB in swap before the reboot); OVR Metrics does not show which process.
- **With a fresh headset the whole run meets < 1 % only when it is long.** The 20-turn run (242 s) is 0.93 %; the default run (106 s) is 1.77 %, because its ~90–110 launch stale frames are the larger part. Turns and idle are under 1 % in both.
- **Release → first audio over 20 consecutive turns: p50 1,804 ms, p90 2,577 ms, max 2,737 ms** (n=20, all turns answered, none over 4 s). Breakdown (p50 / p90 / max): release → ASR final 265 / 278 / 293 ms; ASR final → request 0 / 0 / 5 ms; request → first event 541 / 959 / 1,403 ms; first event → first text 611 / 1,527 / 2,098 ms; first text → sentence queued 0 / 0 / 42 ms; sentence queued → first audio 127 / 333 / 723 ms. By turn type: the greeting (no tool, a full spoken answer) p50 2,362 ms, 6 of 7 over 2.0 s; `come_here` p50 1,387 ms, 0 of 7 over; vision p50 1,806 ms, 3 of 6 over. The p50 meets the 2.0 s budget, the p90 does not; what varies is model time (request → first text p50 1,277 / p90 2,068 / max 2,445 ms), as in every earlier report. Prompt caching held (`cached=1777`–`1905` from turn 2).
- **One default-run greeting took 4,067 ms** (run B), the only turn over 4 s in this session: release → ASR final took 1,096 ms (p50 265 ms) and request → first text 2,772 ms. The other 25 online turns of B and C took 1.1–2.7 s.
- **Launch hitch experiment, measured and reverted.** `PermissionGate` started `ARCameraManager` a frame after the app graph; the experiment started it in `JollyBootstrap.Start` when every permission was already granted, expecting the ~280 ms stall to land before the first visible frame. Three launch-only runs of each build, interleaved (A, B, A, B, A, B; stopped after `offline_greeting`, $0):

  | | Current (camera after the graph) | Camera in `Start` when pre-granted |
  |---|---|---|
  | Worst frame the app measured (`FrameSpikeLog`) | 279, 278, 290 ms | 71, 72, 78 ms |
  | Compositor: frames lost in the scene-start hitch (72 − FPS) | 35, 37, 38 | 37, 36, 38 |
  | Longest run of consecutive stale frames after the first visible frame | 22, 22, 23 (plus 15, 16, 15) | **35, 34, 38** |
  | Stale frames, launch window | 88, 85, 88 | 79, 59, 89 |

  The scene's first frame is not under the OS loading screen: the Unity splash is drawn at 66–72 fps for ~3 s before `Start`, so the stall still freezes a visible frame. The app's own worst frame "improved" only because the stall moved before `FrameSpikeLog` starts. The compositor lost the same ~36 frames, now as one ~0.5 s freeze instead of two shorter ones, so the change was reverted. The "worst launch frame 280 ms" figure of earlier reports undercounts the launch: the compositor sees ~36 frames lost around scene start.

Spend: 3 runs (A $0.0241, B $0.0276, C $0.0845) plus one run whose flag the app could not read (it hung at boot, no paid call; fixed in `751acba`), launch-only runs free; `api-spend.md` has the row.

## SAM text-prompt tools under the self-test, hand debounce (6f0383c)

`main` with `8d34dba` (hand meshes follow a 200 ms debounced tracked state) and `6f0383c` (`vision_highlight_part`, `vision_find_object`; ADR-0033). `tools/ci/test.sh` (EditMode 1050/1050, PlayMode 351/351, pure logic 92.6 %, 0 `warning CS`), `tools/device/build.sh` (IL2CPP ARM64 release, 59 MB, `QuestBuild` 0 errors / 5 warnings, SHA-256 `843ed2100a3a…`), then one `tools/device/selftest.sh` (13:03 UTC). That build stays installed (installed APK SHA-256 matches) and `guardian_pause` read back 0. Dark room (mean luma 7), no Space Setup.

| Scenario | Result |
|---|---|
| `offline_greeting` | PASS, caption 28 ms |
| `voice_greeting` | DEGRADED, first audio 3,165 ms, no tool |
| `voice_come_here` | DEGRADED, 2,375 ms, `navmesh=fallback`, `come_here` |
| `vision_capture_and_identify` | DEGRADED (still), 2,159 ms; tool → answer audio 1,874 ms, mask 1,238 ms |
| `vision_highlight_part` | **PASS**: "Which part of the the mug should I hold?" → `highlight_part`, SAM text prompt on the identify turn's capture (`frame=vision_still`, `sam.done ms=512 prompt=Text objects=1`); tool → part mask **514 ms**, tool → answer audio **1,332 ms**, release → first audio 1,264 ms. "Hold it by the handle on the side. That keeps your fingers safe from the heat." |
| `vision_find_object` | DEGRADED: "Nubi, where is my mug? Show me." → the model called `look_at`, not `find_object` (`found=0`, no SAM call); "Your mug is right there. I'm looking at it now." First audio 2,500 ms |
| `barge_in` | PASS, silence 42 ms |
| `perf_idle` | PASS, 72.0 fps, p95 13.9 ms, 15 draw / 13 SetPass, 33,536 tris, 546 MB |
| Overall, spend | DEGRADED, $0.0255 |

| Metric | Before (4d228c8, 4 runs) | This run |
|---|---|---|
| `evt=hands.tracked` lines per run (nobody wearing the headset) | 38–56 (each flip toggled the hand mesh) | **3**: the launch state, then one phantom left hand that stayed tracked for 320 ms (14:04:56.938 → 57.258) and so passed the 200 ms hold |
| `evt=hands.visual` changes after launch | one per flip | 2 (that one phantom) |

Findings:

- **`highlight_part` works on the headset.** The part mask arrived 514 ms after the tool started, on the same `EncodedImage` the identify turn captured, and the spoken answer 1.3 s after the tool start, inside the 2.5 s DoD budget. This is the second half of the demo's vision beat, now with device evidence.
- **`find_object` was not exercised.** With the mug already in scene memory from the identify turn, the model answered "where is my mug?" with `look_at` on the remembered object instead of a fresh search. The scenario reports DEGRADED with the tool name, as designed. speculation: asking for an object the session has not seen yet, or running the scenario before the identify turn, would make `find_object` the obvious choice; the ordering stays as is for now because the remembered-object answer is reasonable behaviour.
- **Phantom hand flicker: 38–56 → 1 event.** The 200 ms hold dropped every blip but one that lasted 320 ms. A longer hold would drop that too, at the cost of a later real hand; the release-checklist hands item (with a wearer) decides.
- **Compositor stale frames 3.73 % over the whole run** (288 of 7,723), against 2.79–3.24 % in the 3e99cb5 runs. The run is about 20 s longer (two more turns). Not split into launch / turns / idle for this single run. speculation: the same compositor-side rise as in the 3e99cb5 section; the per-frame hand read is two property reads per frame.

## Cartoon voice merged, character voices, device-regression fixes (3e99cb5)

`main` after the `feat/cartoon-voice` merge (`1b50bca`), the voice on `CharacterDefinition` (`1ad725f`), the greeting fix (`b87028a`), the text-done speech break (`5fee08e`) and the launch changes (`b504f60`, `3e99cb5`). `tools/ci/test.sh` (EditMode 1050/1050, PlayMode 332/332, pure logic 92.6 %, 0 `warning CS`), `tools/ci/build.sh` (IL2CPP ARM64 release, 59 MB, `QuestBuild` 0 errors, SHA-256 `91be93a11668…`), then `tools/device/selftest.sh` three times (13:30, 13:32, 13:34 UTC). Then `tools/device/build.sh --character jolly` (64 MB, `[CharacterSceneProcessor] … character=jolly local=1`) and one self-test (13:37 UTC). The default build was reinstalled afterwards (installed APK SHA-256 matches) and `guardian_pause` read back 0. Same dim room (mean luma 16–19, vision used the bundled still), no Space Setup. Before these, one development run re-recorded the pipeline collection and three release runs of `b504f60` (managers staggered, camera still on the graph's frame) are listed as "b504f60".

| Scenario | Run 1 | Run 2 | Run 3 | Jolly |
|---|---|---|---|---|
| `offline_greeting` | PASS, caption 15 ms | PASS, 14 ms | PASS, 13 ms | PASS, 27 ms (Jolly's own line, Railey) |
| `voice_greeting` | DEGRADED, 2,972 ms, no tool | DEGRADED, 2,026 ms, no tool | DEGRADED, 2,346 ms, no tool | DEGRADED, 2,346 ms, no tool, "I'm Jolly, your friendly little companion…" |
| `voice_come_here` | PASS, 1,999 ms, `walk=Arrived` (+ `play_gesture`) | PASS, 1,680 ms | DEGRADED, 2,181 ms | DEGRADED, 2,166 ms |
| `vision_capture_and_identify` | **FAIL**: the Spark vision call failed (`llm.failed kind=Network status=0`), still; tool → answer audio 2,055 ms, mask 794 ms | DEGRADED (still); 1,608 ms, mask 626 ms | DEGRADED (still); 1,917 ms, mask 614 ms | DEGRADED (still); 1,806 ms, mask 1,349 ms |
| `barge_in` | PASS, silence 55 ms | PASS, 33 ms | PASS, 50 ms | PASS, 49 ms |
| `perf_idle` | PASS, 72.0 fps, p95 13.9 ms, 17 draw / 15 SetPass, 33,744 tris, 545 MB | PASS, 72.0 fps, 17 / 15, 33,744, 543 MB | PASS, 72.0 fps, 17 / 15, 33,744, 546 MB | PASS, 72.0 fps, 17 / 15, 33,432, 567 MB |
| Overall, spend | FAIL, $0.0168 | DEGRADED, $0.0109 | DEGRADED, $0.0121 | DEGRADED, $0.0138 |

| Metric | Target | 3e99cb5 (3 runs) | b504f60 (3 runs) | 4d228c8 (3 runs) |
|---|---|---|---|---|
| Release → first audio, online turns | p50 ≤ 2.0 s | p50 **1,776**, 1,282–2,971 ms (n=12) | p50 1,998, 942–3,578 (n=12) | p50 2,090, 1,459–3,971 (n=12) |
| Request → first event / first event → first text | — | 402–1,470 (p50 968) / 0–930 (p50 368) ms | 388–1,847 / 26–1,527 ms | 429–1,220 / 195–1,943 ms |
| First text → first sentence queued | — | 0–68 ms (p50 0) | 0–55 ms (p50 0) | 41–235 ms (p50 112) |
| First sentence queued → first audio | — | 94–416 ms (p50 205) | 98–390 ms (p50 194) | 97–399 ms (p50 147) |
| Greeting turns that called a movement tool | 0 | **0 of 3** (Jolly 0 of 1) | 0 of 3 | 2 of 3 (4 of 7 in the last three reports) |
| Worst launch frame | ≤ ~100 ms | **282, 285, 284 ms** (Jolly 284) | 325, 330, 327 ms | 410, 403, 410 ms |
| Stale frames, launch (to first scenario + 2 s) | — | 90, 96, 92 frames | 82, 90, 86 | 96, 100, 98 |
| Stale frames, turns / idle / whole run | < 1 % | 1.70 / 0.92 / 2.79, 2.07 / 1.30 / 3.24, 1.48 / 1.16 / 2.85 % | 0.73 / 0.25 / 1.82, 1.10 / 0.45 / 2.24, 1.31 / 0.49 / 2.31 % | 0.67 / 0.04 / 1.95, 0.69 / 0.22 / 2.16, 0.79 / 0.21 / 2.22 % |
| Idle draw / SetPass / triangles / memory | ≤ 200 / ≤ 60 / ≤ 300k / — | 17 / 15 / 33.7k / 543–546 MB | 17 / 15 / 33.7k / 543–546 MB | 17 / 15 / 33.7k / 540–543 MB |

Findings:

- **The greeting no longer walks.** "Hi Nubi, who are you?" called no tool in 7 of 7 runs on the new prompt (development, b504f60 ×3, 3e99cb5 ×3) and in the Jolly run, against 4 of 7 before. The static rules say to move only when asked, `come_here` / `follow_user` say they need the user's request, and the companion prompt offers movement "when the person asks". `voice_come_here` still calls `come_here` in every run. The self-test now degrades a greeting that starts a movement tool (`TurnChecks.StayedPut`).
- **Release → first audio p50 1,776 ms (n=12) on the final build, 1,991 ms over all 24 turns of both builds.** The measured client-side win is first text → sentence queued: p50 112 → 0 ms (max 235 → 68 ms), from speaking a preamble when the Responses message item ends (`TextDone` → `SpeechBreak`) instead of after the tool call's arguments, and the greeting losing its tool round. The rest is model time (request → first event + reasoning before the first text, 0.4–2.4 s) and varies run to run; the p50 sits right at the budget, so n=12 does not prove it stays under it. Prompt caching already works: the first turn of a launch is cached except right after a prompt change (`cached=0` on the first run of a new build, 1,777 of 1,813 tokens otherwise), and request size grew only with the context block and history, which come after the cached prefix. No request trimming was done: cached tokens are not where the time goes.
- **Launch worst frame 410 → 282–285 ms; the ~100 ms target is not reached.** Cause, from the frame-spike lines and the log lines inside the spike (development run 13:15:48): one frame held the app-graph build, the AR camera manager start (Meta's `AndroidCameraManager` finding and opening both Camera2 streams, 130 ms, plus ~150 ms before its first log line), every scene manager start including the occlusion manager, and the depth texture creation. `PermissionGate` now starts the camera one frame after the graph is built and each scene manager on its own frame; the old pipeline collection predated the hand rig and occlusion (no hand, depth-occluder or depth-preprocessing pipelines), so it was re-recorded (15 variants / 14 states, was 10 / 9). What remains is the camera manager's own start, one synchronous call into the Meta OpenXR provider that also creates the passthrough layer, so it cannot be deferred or split from app code. The launch window still has ~90 stale frames (the launch is spread over more, shorter frames).
- **Stale frames rose during this session in turns and idle, not only at launch** (idle 0.04–0.22 % in the 4d228c8 runs, 0.25–0.49 % in b504f60, 0.92–1.30 % here, 2.21 % in the Jolly run, which was last). The app's main thread had no frame over 28 ms during `perf_idle` in any of these runs, and app GPU time in idle rose run by run in the same session (4.4 → 4.7 → 5.0 ms, GPU 0.39 → 0.43) at the same clock levels, with nothing drawn differently. speculation: compositor-side (passthrough, headset warm after ~12 back-to-back runs, 37–38 °C); a run on a cooled headset would separate it from the build.
- **Vision call network failure (run 1).** The Spark vision request failed with `kind=Network status=0` after SAM had returned (784 ms); the scenario FAILs as it should since 6cd06e6, and the agent still answered 2.1 s after the tool start. No other run saw it. speculation: transient Wi-Fi or server reset.
- **Jolly speaks with its own voice.** `--character jolly` selects Jolly (`evt=character.selected id=jolly`), the offline greeting is its own clip, and all live speech uses its `VoiceProfile` (Wit Railey, pitch 112, speed 105) through `PersonaRepository`, including the launch Wit warm-up (`warmup.tts status=200`, 452 ms). First sentence → audio was 291–319 ms in that run (n=4), against p50 205 ms for Nubi; speculation: Railey's first chunk, too few turns to tell.

Spend: 8 runs (development, 3 × b504f60, 3 × 3e99cb5, Jolly), app meter $0.104; `api-spend.md` has the row.

## Final verification of main (4d228c8)

`tools/ci/test.sh` (EditMode 1030/1030, PlayMode 312/312, pure logic 92.6 %, 0 `warning CS`), `tools/ci/build.sh` (IL2CPP ARM64 release, 59 MB, `QuestBuild` 0 errors / 2 warnings, SHA-256 `b6dd6e49…`), installed, `push-secrets.sh`, then `tools/device/selftest.sh` three times (11:43, 11:45, 11:46 UTC). Then `tools/device/build.sh --character jolly` (63 MB, `[CharacterSceneProcessor] … character=jolly local=1`) and one self-test (11:49 UTC). The default build was reinstalled afterwards (installed APK SHA-256 matches) and `guardian_pause` read back 0. Same dim room (mean luma 16–19, vision used the bundled still), no Space Setup.

| Scenario | Run 1 | Run 2 | Run 3 | Jolly |
|---|---|---|---|---|
| `offline_greeting` | PASS, caption 28 ms | PASS, 8 ms | PASS, 28 ms | PASS, 28 ms (Jolly's own line) |
| `voice_greeting` | DEGRADED, 3,971 ms (called `come_here`) | DEGRADED, 2,803 ms (called `come_here`) | DEGRADED, 2,178 ms | DEGRADED, 2,161 ms, "I'm Jolly, your little friendly companion…" |
| `voice_come_here` | **PASS**, 1,958 ms, `navmesh=fallback walk=Arrived` | DEGRADED, 2,763 ms, `walk=Arrived` | DEGRADED, 2,068 ms, `walk=Arrived` | PASS, 1,957 ms, `walk=Arrived` |
| `vision_capture_and_identify` | DEGRADED (still; 2,510 ms); tool → answer audio 1,525 ms, mask 1,100 ms | DEGRADED (still); 1,570 ms, mask 1,809 ms | DEGRADED (still); 1,626 ms, mask 1,460 ms | DEGRADED (still); 1,848 ms, mask 1,238 ms |
| `barge_in` | PASS, silence 40 ms | PASS, 52 ms | PASS, 52 ms | PASS, 28 ms |
| `perf_idle` | PASS, 72.0 fps, p95 13.9 ms, 17 draw / 15 SetPass, 33,746 tris, 543 MB | PASS, 72.0 fps, 17 / 15, 33,746, 543 MB | PASS, 72.0 fps, 17 / 15, 33,744, 540 MB | PASS, 72.0 fps, 17 / 15, 33,432, 561 MB |
| Overall, spend | DEGRADED, $0.0146 | DEGRADED, $0.0122 | DEGRADED, $0.0102 | DEGRADED, $0.0135 |

No scenario FAILed, no Responses first-event timeout, no hold line (request → first event 429–1,220 ms in all 16 rounds).

| Metric | Target | Default build, 3 runs | dd14847 (3 runs) |
|---|---|---|---|
| Release → first audio, online turns | p50 ≤ 2.0 s | p50 **2,090**, 1,459–3,971 ms (n=12) | p50 1,833 (n=11) |
| Request → first event / first event → first text | — | 429–1,220 / 195–1,943 ms | 830–1,163 / 539–1,040 (first turns) |
| First sentence queued → first audio | — | 97–399 ms; `warmup.tts` 698, 311, 521 ms | 99–543 ms |
| Tool start → answer audio | p50 ≤ 2.5 s | 1,525, 1,570, 1,626 ms | 2,458, 1,959, 8,332 ms |
| Tool start → mask | p50 ≤ 2.0 s | 1,100, 1,809, 1,460 ms | 930, 653, 598 ms |
| Stale frames, turns / idle / whole run | < 1 % | 0.67 / 0.04 / 1.95, 0.69 / 0.22 / 2.16, 0.79 / 0.18 / 2.22 % | 0.61 / 0.04 / 0.98, 0.69 / 0.18 / 1.59, 0.75 / 0.09 / 1.66 % |
| Stale frames, launch (to first scenario + 2 s) | — | 96, 100, 98 frames; worst app frame 410, 403, 410 ms | 42, 68, 76; worst 332–342 ms |
| Idle draw / SetPass / triangles / memory | ≤ 200 / ≤ 60 / ≤ 300k / — | 17 / 15 / 33.7k / 540–543 MB | 13–22 / 11–13 / 27–35k / 511–521 MB |

Findings:

- **Launch hitch grew (regression).** The whole-run stale share doubled (0.98–1.66 → 1.95–2.22 %) while turns and idle are unchanged; the difference is the launch window, about 30–55 more stale frames, and the worst app frame went from ~340 to ~410 ms. In every run the 410 ms frame lands between `evt=hands.subsystem state=running` and `evt=occlusion.mode mode=depth` (e.g. 12:43:43.861 / .878 / .882). speculation: the environment-depth start or the hand mesh/visualizer activation on that frame; a development-build Profiler capture of the launch would tell which. Turns stay under 1 %.
- **Phantom hand tracking.** With nobody wearing the headset, `evt=hands.tracked left=true` flips on and off about once a second (56, 43, 38 and 46 times per run), each time toggling the hand visual. The self-test does not forward the pinch, so no turn was affected. Whether it shows with a wearer is a release-checklist item (hands, below).
- **Walking on the fallback floor works headless.** `voice_come_here` now reports `navmesh=fallback walk=Arrived` in all four runs (was DEGRADED, no NavMesh); `room.status status=Unscanned` and `room.degraded walkable=fallback radius_m=1.5` ~8 s after launch.
- **Occlusion starts in depth mode.** `occlusion.mode mode=scene_meshes` at start, then `mode=depth` ~0.4 s later, no occlusion errors.
- **Release → first audio p50 is over 2.0 s** (2,090 vs 1,833 ms). TTS is not the cause (first sentence → audio p50 ~150 ms); the spread is model time (request → first text 1.2–3.2 s), and runs 1–2's greeting called `come_here` as before. speculation: the room tools and context line add ~100–200 input tokens per round (in 1,814–2,219, mostly cached); n=12 is too small to separate that from server variance.
- **Mask slower** (1.1–1.8 s vs 0.6–0.9 s), still under 2.0 s. speculation: SAM server latency; nothing on the client path changed.
- **Jolly** (`--character jolly`): `evt=character.selected id=jolly`, it introduces itself as Jolly in both voice turns ("I'm Jolly, your little friendly companion here at home…", "I'm Jolly, by the way."), plays its own offline line, and memory is ~20 MB higher (561 MB). The self-test clips still say "Nubi"; ASR heard "newbie"/"Nobi" and Jolly answered anyway.

Spend: 4 runs, app meter $0.0505; `api-spend.md` has the row.

## Device-finding fixes: late Responses, self-test verdicts, cold Wit (3f35f7f, 6cd06e6, 4e298d8)

`tools/device/build.sh` (IL2CPP ARM64 release, 0 `Assets/Jolly` diagnostics), `push-secrets.sh`, `selftest.sh` headless; `guardian_pause` read back 0. "Before" is `6cd06e6` (fixes 1 and 2), "after" adds the Wit warm-up. The dim room (mean luma 18–19) switched vision to the bundled still in every run.

| Metric | Before (1 run) | After (5 runs) | dd14847 (3 runs) |
|---|---|---|---|
| First sentence queued → first audio, first turn | 417 ms | 417, 140, 180, 222, 402 ms (p50 222) | 472, 166, 543 ms |
| Same, later turns | 122–266 ms | 109–527 ms (p50 167) | 99–278 ms |
| `evt=warmup.tts` (now a discarded synthesis) | 609 ms (`/voices`) | 524, 359, 310, 369, 321 ms | 589–635 ms |
| Release → first audio, first turn | 3,177 ms | 2,444, 2,763, 2,524, 2,330, 3,162 ms | 2,126–2,876 ms |
| Tool start → answer audio | 2,138 ms | 1,722, 2,097, 1,358, 2,235, 1,848 ms | 1,959–8,332 ms |
| Responses first-event timeouts / hold lines | 0 / 0 | 0 / 0 | 3 timeouts |
| Overall | DEGRADED | DEGRADED ×5 (no FAIL) | FAIL, DEGRADED, DEGRADED |

What was found and done:

- **Late Responses first event (3f35f7f).** Request → first event was 0.3–1.4 s in all 52 first rounds logged on the device, but three calls in the dd14847 runs got nothing for 6 s; with 2 attempts that was 12 s of silence after the release acknowledgement before the error line. A round with no first event after 2 s now raises `AgentEventKind.Delayed` and Nubi says a canned Acknowledge line ("One moment."), once more 4 s later during the retry; the first-event timeout is 4 s (a double stall ends in the Error line after ~8 s, and the vision retry now fits its 8 s budget). Config: `voiceFirstEventSeconds`, `holdLineSeconds`, `holdLineRepeatSeconds`. No stall happened in these six runs, so the path is covered by EditMode tests only (late first event, timeout then retry success, timeout then retry failure, with the real `ResponsesChatModel` over `FakeHttpTransport`). UnityWebRequest has no per-request connection control, so the retry cannot ask for a fresh connection; the aborted attempt's connection is not reused.
- **Self-test false pass (6cd06e6).** `capture_and_identify:failed` counted as completed and latency budgets were notes. `TurnChecks`: a failed key tool FAILs, another failed tool and release → first audio > 2.0 s or tool → answer audio > 2.5 s DEGRADE with the reason, no answer after the tool FAILs. The first voice turn is now DEGRADED in every run, which is what it is.
- **Cold first sentence (4e298d8).** The connection was kept: from the dev Mac (Python, one kept-alive connection, 13 trials, 2 s and 10 s idle gaps) the first `/synthesize` after `/voices` took p50 143 ms, after a synthesized "Hi." 88 ms, later requests 83 ms; the reused connection only skipped TLS. The synthesis path itself was cold. The warm-up now synthesizes "Hi." in the persona's voice through the normal streaming request. On the Quest the first-turn p50 halved, but 2 of 5 runs were still ~400 ms. speculation: per-request Wit variance and Quest Wi-Fi power saving after ~10 s idle; later turns also reach 348–527 ms now and then. Whether UnityWebRequest uses HTTP/2 to api.wit.ai cannot be read from the player logs.

Spend: 6 runs, app meter $0.0764; `api-spend.md` has the row.

## Release runs of main after the merges (dd14847)

`tools/device/build.sh` (IL2CPP ARM64 release, 58 MB, `QuestBuild` 0 errors / 2 warnings), installed APK verified by SHA-256, `push-secrets.sh`, then `tools/device/selftest.sh` three times (09:39, 09:41, 09:43 UTC). `guardian_pause` read back as 0 afterwards. Release players do not log GC per frame (`gc_bytes_per_frame=NaN`); the turn GC numbers stay those of the development runs below.

| Scenario | Run 1 | Run 2 | Run 3 |
|---|---|---|---|
| `offline_greeting` | PASS, caption 41 ms | PASS, 27 ms | PASS, 41 ms |
| `voice_greeting` | PASS, 2,126 ms | PASS, 2,166 ms (called `come_here`) | PASS, 2,876 ms (called `come_here`) |
| `voice_come_here` | **FAIL**: two Responses timeouts, no reply | DEGRADED (no NavMesh), 1,360 ms | DEGRADED (no NavMesh), 2,194 ms, similarity 0.857 |
| `vision_capture_and_identify` | PASS, live frame; tool → answer audio 2,458 ms, mask 930 ms | PASS; 1,959 ms, mask 653 ms | PASS, but the vision call timed out (`tools=capture_and_identify:failed`); tool → answer audio 8,332 ms, mask 598 ms |
| `barge_in` | PASS, silence 40 ms | PASS, 55 ms | PASS, 41 ms |
| `perf_idle` | PASS, 72.0 fps, p95 13.9 ms, 22 draw / 13 SetPass, 35,194 tris, 521 MB | PASS, 72.0 fps, 13 / 11, 27,380 tris, 511 MB | PASS, 72.0 fps, 13 / 11, 27,252 tris, 513 MB |
| Overall, spend | FAIL, $0.0127 | DEGRADED, $0.0128 | DEGRADED, $0.0126 |

| Metric | Target | These runs | fix/turn-gc (for comparison) |
|---|---|---|---|
| Release → first audio, all online turns | p50 ≤ 2.0 s | p50 **1,833**, max 2,876 ms (n=11; 1,083–2,876) | p50 1,750, max 2,512 (n=12, development) |
| Release → first audio, first turn of the launch | p50 ≤ 2.0 s | **2,126, 2,166, 2,876 ms** | 1,718–1,845 (development); 2,249 (one release run) |
| First sentence queued → first audio, first turn | — | 472, 166, 543 ms (later turns 99–278) | 194–320 (development); 221 (release) |
| Tool start → answer audio | p50 ≤ 2.5 s | 2,458, 1,959, 8,332 ms (p50 2,458) | — |
| Stale frames, turns / idle / whole run | < 1 % | 0.61 / 0.04 / 0.98, 0.69 / 0.18 / 1.59, 0.75 / 0.09 / 1.66 % | turns 0.64–0.82 % |
| Stale frames, launch (first scenario + 2 s) | — | 42, 68, 76 frames (worst frame 332–342 ms, camera start) | 33–78 frames |
| GC per frame in turns | ≤ 4 KB | not logged in release players | p50 3.9, max 6.4 KB |

Findings (no code changed):

- **Meta Responses timeouts.** Three calls got no first SSE event within 6 s (`evt=net.retry … kind=Timeout status=0`, then `llm.failed … no first stream event after 6 s`): both attempts of the `voice_come_here` agent call in run 1 (09:40:15 → 09:40:27 UTC), which made that scenario FAIL, and the vision call in run 3 (09:43:51 → 09:43:59 UTC). No response ids were returned. No earlier run in this report logged one. speculation: server-side latency, since the other ~20 Spark calls in these runs had their first event 0.5–1.2 s after the request.
- **The self-test passes a vision turn whose tool failed.** Run 3 reports `vision_capture_and_identify` PASS with `tools=capture_and_identify:failed` and an 8.3 s answer.
- **The first turn of a launch is over 2.0 s in all three runs.** Request → first event took 830–1,163 ms and first event → first text 539–1,040 ms. In runs 1 and 3, first sentence queued → audio was 472 and 543 ms, the cold-Wit range from before ADR-0023, although `evt=warmup.tts status=200` (589–635 ms) ran at every launch. Later turns took 99–278 ms.
- The greeting called `come_here` in runs 2 and 3, as in the fix/turn-gc release run.

## Turn GC and the cold first turn (fix/turn-gc)

Development IL2CPP players (`NUBI_DEVELOPMENT=1`), `tools/device/selftest.sh`; "before" is `main` `f19b36e` plus the profiling switch (inert without `--profile`). GC is `evt=selftest.frames gc_bytes_per_frame` per turn scenario (`offline_greeting`, `voice_greeting`, `voice_come_here`, `vision_capture_and_identify`, `barge_in`), all threads. Stale frames: VrApi, turns phase (first scenario + 2 s to `perf_idle`) and whole run (includes the ~350 ms launch frame).

| Metric | Target | Before (3 runs) | After (3 runs) |
|---|---|---|---|
| GC per frame in turns, p50 / max over 15 scenario windows | ≤ 4 KB | **14.5 / 38.0 KB** | **3.9 / 6.4 KB** |
| `voice_greeting` | ≤ 4 KB | 13.1, 14.5, 17.0 KB | 3.7, 4.3, 3.3 KB |
| `voice_come_here` | ≤ 4 KB | 22.0, 21.1, 21.0 KB | 3.9, 4.6, 4.1 KB |
| `vision_capture_and_identify` | ≤ 4 KB | 14.0, 12.7, 12.9 KB | 3.1, 3.6, 3.3 KB |
| `barge_in` (two ASR sessions) | ≤ 4 KB | 37.0, 38.0, 36.9 KB | 5.9, 6.4, 6.0 KB |
| `offline_greeting` | ≤ 4 KB | 4.1, 3.9, 4.0 KB | 3.9, 3.8, 3.5 KB |
| GC per frame, `perf_idle` | 0 B | 385, 384, 384 B | 378, 386, 386 B |
| Release → first audio, first turn of the launch | p50 ≤ 2.0 s | 3,764, 2,888, 2,177 ms | 1,845, 1,819, 1,718 ms |
| Release → first audio, all online turns | p50 ≤ 2.0 s | p50 2,036, max 3,764 (n=12) | p50 1,750, max 2,512 (n=12) |
| First sentence queued → first audio, first turn | — | 612, 333, 443 ms | 320, 194, 195 ms |
| First ASR ack of a launch | — | 1,940, 1,537, 1,644 ms | 1,428, 1,330, 1,147 ms |
| Stale frames, turns / whole run | < 1 % | 0.82 / 1.65, 0.91 / 1.79, 0.69 / 1.78 % | 0.66 / 1.76, 0.82 / 1.50, 0.64 / 1.71 % |

Release verification (IL2CPP release, 09:26 UTC, one run): every scenario passed except `voice_come_here` (DEGRADED, no NavMesh); ASR over the new WebSocket path works under release stripping; release → first audio 2,249 / 2,430 / 1,081 / 1,777 ms (the first two turns had 1.0 s and 0.7 s of model reasoning before the first text, and the greeting called `come_here`), first sentence 221 ms, stale frames 0.92 % in turns, 1.19 % whole run, 72.0 fps idle.

What was found and done (ADR-0023):

- **Measured, not guessed.** `selftest.sh --profile` records each turn scenario to a Profiler log with allocation call stacks; `GcAllocReport` attributes the bytes in batchmode (docs/perf/README.md §7). Its totals match the frame log within 1 %.
- **72 % of all turn garbage was the ASR WebSocket connect.** Mono's `ClientWebSocket` reads the HTTP 101 head one byte per TLS read, and Mono's TLS stream allocates a fresh 16 KB buffer per read. `ClientWebSocketConnection` now opens socket, TLS and the upgrade itself and hands the stream to `WebSocket.CreateFromStream`.
- **Every TLS send and receive still costs 16 KB** (Mono's `MobileAuthenticatedStream`). Audio now goes out in ≥ 240 ms frames, a third of the sends; receives are one per server message and stay. This is most of what remains, and why `barge_in`, with two sessions in ~7 s, is still over.
- **Main-thread allocators fixed:** SSE parser buffers (reused, and kept across streams), the Responses `created`/`completed` echo (only id, usage, incomplete_details and error are read), `ArrayPool.Shared` re-renting on the PCM paths (dedicated pools), and the self-test converting its clips inside each turn (now at launch).
- **Left as is, third-party:** XRI `CurveVisualController` copies its gradient every frame a ray is visible (~0.3 KB/frame in every turn profile; likely most of the ~380 B/frame of `perf_idle` in development players, which was not profiled), TMP and Input System array growth on new text or bindings.
- **Cold first turn.** The Wit connection was cold: the first sentence of a launch took 333–612 ms from queued to first audio against 95–208 ms for turns 3–4. A `GET /voices` at launch (synthesizes nothing) brings it to 194–320 ms. The Responses connection was already warm from the launch health probe: request → first event on the first turn was 466–1,345 ms against 513–1,096 ms later, before any change. The 3.1 s first turn of the release audit was mostly model reasoning (first event → first text 1.5 s, 450 output tokens); in the three "before" runs here it was 1.0–1.4 s on the first turn, which no warm-up removes.

Spend: 12 self-test runs (3 before, 1 intermediate, 3 after, 4 profiled, 1 release; app meter $0.149 in total). `api-spend.md` has the row.

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
- **GC during turns is over budget.** Development players measured 11–42 KB per frame during turns (DoD: ≤ 4 KB) and ~200 B per frame idle. Not addressed in this pass (see the fix/turn-gc section above).

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
- GC allocation per frame in release players: "GC Allocated In Frame" is recorded only in development players; the release run reports collections (0 in 30 s). The turn numbers above are from development players.
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
| GC alloc | 0 B/frame idle; ≤ 4 KB/frame in a turn | release: 0 collections in 30 s; development player (fix/turn-gc, 3 runs): ~385 B/frame idle (XRI ray gradient), turns p50 3.9 KB/frame, max 6.4 (`barge_in`) | **Met at p50; not in `barge_in` (5.9–6.4 KB) and one `voice_come_here` (4.6 KB)**; idle met in the release player and in `NoGcAllocInSteadyStateIdle` |
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
