#!/usr/bin/env bash
# Device self-test: runs the app's built-in SelfTestRunner on a connected Quest with nobody wearing it, then prints
# a PASS/FAIL table from selftest_results.json. Exits 1 when any scenario FAILs (DEGRADED is reported, exit 0).
# Usage: tools/device/selftest.sh [--build] [--apk path] [--timeout seconds] [--skip-install] [--profile] [--turns N]
#   --profile: development players record each turn scenario to a Profiler log with allocation call stacks, pulled to
#   artifacts/selftest/selftest_turns_<scenario>.raw for Jolly.Editor.Tools.GcAllocReport (docs/perf/README.md §7).
#   --turns N: instead of the scenario list, N consecutive online voice turns (greeting, come here, vision, repeated)
#   and perf_idle; prints release -> first audio p50/p90/max and the stage breakdown. About $0.004 per turn ($0.085 for
#   20 on 2026-11-01; one turn in three is a vision turn with a SAM call).
#   ANDROID_SERIAL selects the device (adb). NUBI_PACKAGE=com.nubi.companion. Results go to artifacts/selftest/.
#   NUBI_TMPDIR: where push-secrets.sh writes its temporary JSON (keep TMPDIR short for Unity builds).
# Spends real Meta API money (about $0.02-0.04 per run: ~7 short Spark turns, 1 vision call, 3-4 SAM images, ASR);
# append the printed spend to docs/api-spend.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PKG="${NUBI_PACKAGE:-com.nubi.companion}"
APK="$ROOT/artifacts/Nubi.apk"
TIMEOUT=
BUILD=0
SKIP_INSTALL=0
PROFILE=0
TURNS=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --build) BUILD=1 ;;
    --apk) APK="$2"; shift ;;
    --timeout) TIMEOUT="$2"; shift ;;
    --skip-install) SKIP_INSTALL=1 ;;
    --profile) PROFILE=1 ;;
    --turns) TURNS="$2"; shift ;;
    *) echo "unknown argument $1" >&2; exit 2 ;;
  esac
  shift
done

[[ "$TURNS" =~ ^[0-9]+$ ]] || { echo "[selftest] --turns takes a count" >&2; exit 2; }
# 20 turns took 207 s on 2026-11-01; the margin covers slow model rounds and the come-here walk.
TIMEOUT="${TIMEOUT:-$(( TURNS > 0 ? 120 + 25 * TURNS : 360 ))}"

OUT="$ROOT/artifacts/selftest"
FILES="/sdcard/Android/data/$PKG/files"
mkdir -p "$OUT"
rm -f "$OUT/selftest_results.json" "$OUT/logcat.txt" "$OUT/graphics_states.graphicsstate" "$OUT"/selftest_turns_*.raw

adb get-state >/dev/null || { echo "[selftest] no device (adb get-state failed)" >&2; exit 2; }
adb wait-for-device

restore_headset() {
  adb shell am force-stop "$PKG" >/dev/null 2>&1 || true
  adb shell am broadcast -a com.oculus.vrpowermanager.automation_disable >/dev/null 2>&1 || true
  adb shell setprop debug.oculus.guardian_pause 0 >/dev/null 2>&1 || true
  echo "[selftest] headset restored (proximity sensor, Guardian)"
}
trap restore_headset EXIT

if [[ $BUILD -eq 1 ]]; then
  "$ROOT/tools/ci/build.sh" "$APK"
  # Unity kills the adb server during a build (device-spike.md §4).
  adb start-server >/dev/null
  adb wait-for-device
fi
if [[ $SKIP_INSTALL -eq 0 ]]; then
  [[ -s "$APK" ]] || { echo "[selftest] missing $APK (use --build)" >&2; exit 2; }
  # The first install right after the adb daemon starts can fail with an empty reason; one retry is enough.
  adb install -r -g "$APK" >/dev/null || { sleep 2; adb install -r -g "$APK" >/dev/null; }
  echo "[selftest] installed $APK"
fi
for p in horizonos.permission.HEADSET_CAMERA android.permission.RECORD_AUDIO com.oculus.permission.USE_SCENE; do
  adb shell pm grant "$PKG" "$p" 2>/dev/null || echo "[selftest] pm grant failed for $p"
done

TMPDIR="${NUBI_TMPDIR:-${TMPDIR:-/tmp}}" "$ROOT/tools/device/push-secrets.sh" >/dev/null
echo "[selftest] secrets pushed"

# Nobody is wearing the headset: wake it, fake proximity, pause the Guardian dialog (device-spike.md §5).
adb shell input keyevent KEYCODE_WAKEUP
adb shell am broadcast -a com.oculus.vrpowermanager.prox_close >/dev/null
adb shell setprop debug.oculus.guardian_pause 1
# The default 256 KiB main buffer rotates in about a minute.
adb logcat -G 16M
adb logcat -c

adb shell am force-stop "$PKG"
adb shell rm -f "$FILES/selftest_results.json" "$FILES/graphics_states.graphicsstate" "$FILES/selftest_turns_*.raw"
if [[ $PROFILE -eq 1 ]]; then adb shell touch "$FILES/selftest_profile"; fi
# Pushed, not written with adb shell echo: the app cannot read a file the shell created there.
FLAG="$(mktemp "${NUBI_TMPDIR:-${TMPDIR:-/tmp}}/selftest.XXXXXX")"
if [[ $TURNS -gt 0 ]]; then echo "{\"turns\":$TURNS}" > "$FLAG"; else echo '{}' > "$FLAG"; fi
adb push "$FLAG" "$FILES/selftest.json" >/dev/null
rm -f "$FLAG"
ACTIVITY="$(adb shell cmd package resolve-activity --brief "$PKG" | tail -n 1 | tr -d '\r')"
adb shell am start -n "$ACTIVITY" >/dev/null
echo "[selftest] launched $ACTIVITY; waiting up to ${TIMEOUT}s"

start=$SECONDS
while ! adb shell test -f "$FILES/selftest_results.json" 2>/dev/null; do
  if (( SECONDS - start > TIMEOUT )); then
    adb logcat -d -v time -s Unity:V > "$OUT/logcat.txt" || true
    echo "[selftest] TIMEOUT after ${TIMEOUT}s; last self-test lines:" >&2
    grep -E "selftest|app.failed|Exception" "$OUT/logcat.txt" | tail -n 25 >&2 || true
    exit 1
  fi
  if ! adb shell pidof "$PKG" >/dev/null 2>&1 && (( SECONDS - start > 20 )); then
    adb logcat -d -v time > "$OUT/logcat.txt" || true
    echo "[selftest] the app is not running; see $OUT/logcat.txt" >&2
    exit 1
  fi
  sleep 3
done

adb pull "$FILES/selftest_results.json" "$OUT/selftest_results.json" >/dev/null
# Development players also record the graphics states the run drew (docs/perf/README.md §7).
adb pull "$FILES/graphics_states.graphicsstate" "$OUT/graphics_states.graphicsstate" >/dev/null 2>&1 || true
if [[ $PROFILE -eq 1 ]]; then
  for raw in $(adb shell ls "$FILES" | tr -d '\r' | grep -E '^selftest_turns_.*\.raw$'); do
    adb pull "$FILES/$raw" "$OUT/$raw" >/dev/null || true
  done
fi
adb logcat -d -v time -s Unity:V > "$OUT/logcat.txt" || true
grep -E "evt=(selftest|turn.latency)" "$OUT/logcat.txt" > "$OUT/selftest_events.txt" || true
# Compositor stale frames over the whole run (DoD §4: < 1%); the app cannot see these itself.
# Only the app's own lines: the home shell logs VrApi too until the app takes over.
APP_PID="$(grep -m1 -oE 'I/Unity +\( *[0-9]+\)' "$OUT/logcat.txt" | grep -oE '[0-9]+' || true)"
adb logcat -d -v time -s VrApi:I | grep -E "\( *${APP_PID:-x}\):" > "$OUT/vrapi.txt" || true
awk '/FPS=/ { split(substr($0, index($0, "FPS=") + 4), f, "/"); frames += f[1]
              if (match($0, /Stale=[0-9]+/)) stale += substr($0, RSTART + 6, RLENGTH - 6) }
     END { if (frames > 0) printf "[selftest] compositor: %d of %d frames stale (%.2f%%, target < 1%%)\n", stale, frames, 100 * stale / frames }' \
  "$OUT/vrapi.txt"

# Phantom hand tracking with nobody wearing the headset (selftest-report.md, 4d228c8: 38-56 lines per run).
echo "[selftest] hand tracking: $(grep -c 'evt=hands.tracked' "$OUT/logcat.txt" || true) evt=hands.tracked lines"

python3 -I - "$OUT/selftest_results.json" <<'PY'
import json, sys
r = json.load(open(sys.argv[1], encoding="utf-8"))
keys = ["release_to_first_audio_ms", "tool_to_answer_audio_ms", "tool_to_mask_ms", "found", "similarity", "caption_ms", "silence_ms", "avg_fps", "p95_frame_ms",
        "draw_calls_max", "setpass_calls_max", "triangles_max", "system_memory_mb",
        "gc_alloc_bytes_per_frame", "mean_luma", "frame", "navmesh", "come_source", "tools"]
print(f"\nSelf-test on {r['device']} at {r['startedUtc']}")
print(f"{'scenario':30} {'status':9} {'ms':>7}  key metrics")
for s in r["scenarios"]:
    m = s["metrics"]
    shown = ", ".join(f"{k}={m[k]}" for k in keys if k in m and m[k] not in ("", "null"))
    print(f"{s['name']:30} {s['status']:9} {s['durationMs']:>7.0f}  {shown}")
    for note in s["notes"]:
        print(f"{'':41}- {note}")
    # The model's own words explain a missed or unexpected tool call (ADR-0036).
    if s["status"] != "PASS" and m.get("llm_reply"):
        print(f"{'':41}  llm: {m['llm_reply']}")
for s in r["scenarios"]:
    m = s["metrics"]
    if "turns" not in m:
        continue
    print(f"\n{s['name']}: {m['turns']} turns, {m['turns_degraded']} degraded; release -> first audio by turn (ms): {m['first_audio_ms_by_turn']}")
    for stage in ["release_to_first_audio", "release_to_asr_final", "asr_final_to_request", "request_to_first_event",
                  "first_event_to_first_text", "first_text_to_sentence", "sentence_to_first_audio"]:
        print(f"  {stage:28} n={m[stage + '_n']:>3} p50={m[stage + '_p50_ms']:>6} p90={m[stage + '_p90_ms']:>6} max={m[stage + '_max_ms']:>6}")
print(f"\nOVERALL {r['overall']}   Meta spend this run: ${r['spentUsd']:.4f} (log it in docs/api-spend.md)")
sys.exit(1 if r["overall"] == "FAIL" else 0)
PY
