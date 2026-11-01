# Profiling Nubi on Quest 3

This page explains how to measure the targets in [`definition-of-done.md`](../definition-of-done.md) §4 (performance) and §5 (latency), and where to put the results.

- Raw captures (CSV, `.data`, logcat dumps) go to `artifacts/`, which is git-ignored.
- Committed summaries go here: `docs/perf/report.md` for frame time, memory and thermals, and [`vision-eval.md`](vision-eval.md) for the R6 vision/SAM evaluation.

Always measure on a **Quest 3** with passthrough on and Nubi visible, during the scripted conversation (the six beats of [`demo-script.md`](../demo-script.md), looped). Record the Horizon OS version (`adb shell getprop ro.vros.build.version`) and the commit in every summary.

## 1. Builds

| Purpose | Build | Command |
|---|---|---|
| Frame rate, thermals, battery, memory (OVR Metrics) | **Release** IL2CPP | `tools/ci/build.sh` → `artifacts/Nubi.apk` |
| Per-frame CPU, draw calls, GC (Unity Profiler, Frame Debugger) | **Development** IL2CPP | `NUBI_DEVELOPMENT=1 tools/ci/build.sh artifacts/Nubi-dev.apk` |

Install either one with `tools/device/install-run.sh <apk>`, after `tools/device/push-secrets.sh`.

A development build carries profiler overhead, so take frame-rate and thermal numbers from the release build only.

Neither build sets `JOLLY_DEBUG_LOG` or `JOLLY_DEBUG_CAPTURE`, and `tools/ci/test.sh` fails if either appears in the committed scripting defines.

## 2. Quick health check (no tools)

The Quest runtime logs one `VrApi` line per second with FPS, CPU/GPU utilisation, app GPU time, free memory and temperature. The device spike used these lines (`docs/research/device-spike.md` §1):

```bash
adb logcat -G 16M                      # Quest's default 256 KiB buffer rotates in about a minute
adb logcat -s VrApi | tee artifacts/vrapi.log
```

Treat this as a smoke check only. It is not a substitute for the OVR Metrics CSV.

## 3. OVR Metrics Tool: frame rate, stale frames, memory, thermals

Meta's reference: https://developers.meta.com/horizon/documentation/unity/ts-ovrmetricstool/

1. Install the **OVR Metrics Tool** on the headset, following that page.
2. In the tool, enable the performance overlay if you want it in a recording, and turn on **CSV recording**.
3. Launch Nubi and run the scripted conversation:
   - **10 minutes** for the frame-rate and stale-frame rows;
   - **30 minutes** for thermal and battery: a turn every ~30 s plus 5 captures.
4. Stop recording. `adb pull` the CSV into `artifacts/perf/` (the tool's settings show its output folder).
5. Summarise the CSV in `docs/perf/report.md`:

| DoD row | CSV fields / source | Report |
|---|---|---|
| Frame rate ≥ 72 fps sustained | FPS column | min, p5, mean |
| Stale frames < 1 % / 10 min | stale-frame column | % of frames |
| Memory ≤ 1,500 MB peak | `app_rss_MB` (also `app_uss_MB`) | peak, mean |
| Thermal: no drop below 72 fps from throttling over 30 min | FPS and temperature / clock-level columns over time | the time of any drop and the clock levels at that point |
| Battery ≤ 20 % / 30 min | battery column, or `adb shell dumpsys battery` (`level`) at start and end | % drained |

Field names follow Meta's page above. If a column name differs on your OS version, write down the name you used.

## 4. Unity Profiler: CPU, rendering, GC

Use the development APK.

1. Open the project in Unity 6000.6.6f1, then **Window → Analysis → Profiler**.
2. Choose the headset in the Profiler's target dropdown. If it doesn't appear, forward the port the way the Unity Android profiling docs describe, then connect to `127.0.0.1`:
   ```bash
   adb forward tcp:34999 localabstract:Unity-com.nubi.companion
   ```
3. Leave **Deep Profile off**: it distorts IL2CPP timings. Record 300+ frames from each phase: idle, listening, speaking, walking, and `capture_and_identify`.
4. Read these targets:

| DoD row | Where |
|---|---|
| CPU main thread p95 ≤ 11 ms | CPU Usage module, main thread. Note p95 across the captured frames per phase |
| Draw calls ≤ 200, SetPass ≤ 60 | Rendering module; confirm with **Window → Analysis → Frame Debugger** connected to the same player |
| Triangles ≤ 300k scene, Nubi ≤ 30k | Rendering module. Nubi is 25,248 triangles by construction (`docs/art/nubi/README.md`) |
| GC alloc: 0 B/frame idle, ≤ 4 KB/frame during a turn, no GC spike > 2 ms | CPU module, *GC Alloc* column in Hierarchy view; `GC.Collect` markers in the timeline |

5. Save the capture (`.data`) to `artifacts/perf/` and copy the numbers into `report.md`.

The idle-allocation target is also enforced without a headset. The PlayMode test `NoGcAllocInSteadyStateIdle` (added with the WP9 integration) records the `GC.Alloc` marker over steady-state idle frames and fails on any byte. If you find an allocation in the Profiler first, turn it into a failing test, then fix it.

## 5. Latency: `evt=turn.latency`

Every turn logs one logfmt line. Each mark is in milliseconds from the first mark of that turn:

```
evt=turn.latency turn=7 PttPressed=0 PttReleased=1480 AsrFinal=1690 LlmRequestSent=1700 LlmFirstEvent=2310 LlmFirstText=2620 FirstSentenceQueued=2700 TtsFirstByte=3240 FirstAudioPlayed=3260 TurnCompleted=6900
```

The marks are the `LatencyMark` enum in `Runtime/Contracts/Common.cs`. A mark is missing when its stage didn't happen (for example, no `MaskShown` on a turn without a capture).

Run 20 consecutive turns on the live-user route over Wi-Fi, then compute p50 and p90 per stage:

```bash
adb logcat -d -s Unity | grep -o 'evt=turn.latency.*' > artifacts/latency.txt

stage() {   # stage FROM_MARK TO_MARK FILE  → n, p50, p90, max in ms
  awk -v a="$1" -v b="$2" '{ f = ""; t = ""; for (i = 1; i <= NF; i++) { split($i, kv, "="); if (kv[1] == a) f = kv[2]; if (kv[1] == b) t = kv[2] } if (f != "" && t != "") print t - f }' "$3" |
    sort -n | awk '{ v[NR] = $1 } END { if (NR == 0) { print "n=0"; exit } p50 = int((NR + 1) * 0.5); p90 = int((NR + 1) * 0.9); if (p90 > NR) p90 = NR; printf "n=%d p50=%d p90=%d max=%d\n", NR, v[p50], v[p90], v[NR] }'
}

stage PttReleased   AsrFinal            artifacts/latency.txt   # target p50 ≤ 300 ms
stage AsrFinal      LlmFirstText        artifacts/latency.txt   # ≤ 1,300 ms
stage LlmFirstText  FirstSentenceQueued artifacts/latency.txt   # ≤ 150 ms
stage FirstSentenceQueued TtsFirstByte  artifacts/latency.txt   # ≤ 400 ms
stage PttReleased   FirstAudioPlayed    artifacts/latency.txt   # ≤ 2,000 ms  (headline number)
stage PttReleased   MaskShown           artifacts/latency.txt   # capture turns: ≤ 2,000 ms
```

The latency marks cannot measure **pinch → silence** for barge-in. Measure that one with the Profiler's Audio module, or record the headset audio together with a visible pinch and count frames. The target is 150 ms.

For **network loss → offline line**, toggle Wi-Fi, pinch, and time the gap to the offline caption. The target is 2 s.

## 6. Reporting

`docs/perf/report.md` gets one section per measured build: commit, OS version, room, then the DoD §4 table with a *Measured* column, and the §5 table with p50/p90.

For every row that misses its target, give the cause and the mitigation (fixed foveation level, refresh rate, mask overlay cost, a cheaper model for small talk). Record any change of target in an ADR.

The README's performance table links here and shows the headline numbers.
