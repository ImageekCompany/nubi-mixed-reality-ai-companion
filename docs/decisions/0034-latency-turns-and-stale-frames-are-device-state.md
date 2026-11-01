# 0034. A 20-turn latency mode; stale frames are device state; the camera keeps its post-graph start

- Status: accepted
- Date: 2026-11-01

## Context
- The DoD §5 claim "release → first audio p50 ≤ 2.0 s" rested on 12 turns from three default self-test runs (p50 1,776 ms, selftest-report.md, 3e99cb5). Each default run has four online voice turns, too few to tell 1.8 s from 2.1 s, and the DoD asks for 20 consecutive turns.
- Compositor stale frames were 3x over the < 1 % target (whole run 2.79–3.73 %, idle 0.9–2.2 %) and rose run by run through the day while the app's main thread showed no frame over 28 ms in idle. The report's guess was heat or the compositor; nothing separated them.
- The worst launch frame (~280 ms) is `ARCameraManager`'s start, one call into the Meta OpenXR provider. `PermissionGate` runs it a frame after the app graph (ADR-0032). Starting it before the first rendered frame when every permission is already granted might hide it under the OS loading screen.

## Decision
- **`selftest.sh --turns N`.** The script pushes `{"turns":N}` as `selftest.json`; `SelfTestOptions` reads it (capped at 50) and `SelfTestRunner` replaces the scenario list with `LatencyTurnsScenario` and `perf_idle`. The scenario rotates the existing greeting, come-here and vision scenarios, so each turn keeps its own checks, and reports n / p50 / p90 / max for release → first audio and each stage of it (`LatencySeries`, nearest rank, in `Jolly.Core.Diagnostics`). A turn that did not return to Idle is not sampled, since the tracker would still hold the previous turn. A failed turn fails the run; a p50 over 2.0 s degrades it. `{}` (the default) runs the default list, unchanged.
- **The flag is `adb push`ed.** The app cannot read a file that `adb shell echo` created in its files folder (`UnauthorizedAccessException`, which hung the boot on the first device run); an unreadable flag now counts as the default run.
- **Stale frames: reboot the headset before a demo; no app change.** On one APK: cooled headset at 4 days 14 h uptime, idle 5.0 %; after `adb reboot`, idle 0.23–0.32 % and turns 0.49–0.60 %. OVR Metrics counted stale frames during the Unity splash, before app code ran, and the app's GPU time for the same idle scene at the same GPU level fell from 5.4 to 3.0 ms across the reboot. The cause is the headset's state, not the build and not heat.
- **The camera manager keeps its post-graph start.** The early start was built and measured on three interleaved launches per build and reverted: the Unity splash is drawn for ~3 s before the scene's `Start`, so the first scene frame is visible and the stall is not hidden. The compositor lost the same ~36 frames (35–38 vs 36–38) as one 34–38-frame freeze instead of two of 15–16 and 22–23 frames; only the app's own `FrameSpikeLog` improved (279–290 → 71–78 ms), because the stall moved before it starts.

## Consequences
- Device numbers (selftest-report.md, 751acba): 20 turns, release → first audio p50 1,804 / p90 2,577 / max 2,737 ms. The p50 meets the budget and the p90 does not; greetings are the slow turns (p50 2,362 ms), and the variance is model time (request → first text p50 1,277, max 2,445 ms).
- A 20-turn run costs about $0.085 (one turn in three is a vision turn with a SAM call) and takes ~4 minutes.
- Turns and idle meet < 1 % only on a recently rebooted headset; a short default run is still ~1.8 % because of the launch hitch, which stays at ~36 lost frames. The demo procedure needs a reboot step.
- `selftest.sh` default behaviour is unchanged except that the flag file is pushed.
- Earlier reports' "worst launch frame" is the app's view; the compositor sees ~0.5 s of lost frames around scene start.

## Alternatives
- **A separate latency script that parses `evt=turn.latency` from logcat.** Rejected: the scenario already drives the turns; computing stats in the app keeps one results file and one report format, and `LatencySeries` is testable in EditMode.
- **A flag file name carrying the count (`selftest_latency_20`).** Not needed once the flag is pushed; JSON leaves room for other options.
- **Lower render cost or performance-level hints to absorb the stale frames.** Not justified: after a reboot the app is under 1 % in turns and idle at the lowest CPU/GPU levels; the excess came with uptime.
- **Keep the early camera start for its better `FrameSpikeLog` number.** Rejected: the user sees the compositor, where the freeze got longer.
