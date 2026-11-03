#!/usr/bin/env bash
# Quest system recording for the demo (docs/demo-script.md). Verified on Quest 3, Horizon OS v209
# (metacam 209.0.0.x) on 2026-11-01:
#   - The recorder (com.oculus.metacam) reads debug.oculus.capture.{width,height,fps,bitrate,codec} (names found in its
#     v209 code). setprop needs no root and is lost on reboot, so run `props` after every reboot (ADR-0034).
#   - With none set, a recording of this app came out 1920x1080 HEVC, 60 fps, ~7.6 Mbit/s, AAC 48 kHz stereo, with
#     passthrough in the frames, in /sdcard/Oculus/VideoShots/<package>-<yyyyMMdd-HHmmss>-0.mp4.
#   - Starting or stopping it over adb does not work: the only intent the recorder takes
#     (horizonos.appactions.START_RECORDING / STOP_RECORDING on .capture.CaptureService) needs a result Messenger
#     that `am` cannot pass, and logs "Missing result messenger". Start and stop it in the headset (below).
# Usage:
#   tools/demo/record.sh props [WxH] [fps] [bitrate]  set and read back the capture props (default 1920x1080 30 20000000)
#   tools/demo/record.sh list                         the newest recordings on the headset
#   tools/demo/record.sh pull [dest]                  copy the newest recording to dest (default artifacts/demo/raw/)
#   tools/demo/record.sh screen [seconds] [dest]      adb screenrecord: the app's two lens-distorted eye buffers,
#                                                     4128x2208, WITHOUT passthrough (measured); a debugging aid only
#   tools/demo/record.sh start|stop                   print how to start or stop the recorder in the headset
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SHOTS=/sdcard/Oculus/VideoShots
cmd="${1:-}"
[[ -n "$cmd" ]] || { sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }
shift
adb get-state >/dev/null || { echo "[record] no device" >&2; exit 2; }

case "$cmd" in
  props)
    size="${1:-1920x1080}"; fps="${2:-30}"; bitrate="${3:-20000000}"
    adb shell setprop debug.oculus.capture.width "${size%x*}"
    adb shell setprop debug.oculus.capture.height "${size#*x}"
    adb shell setprop debug.oculus.capture.fps "$fps"
    adb shell setprop debug.oculus.capture.bitrate "$bitrate"
    adb shell getprop | grep "debug.oculus.capture" | sed 's/^/[record] /'
    echo "[record] set; they apply to the next recording and are gone after a reboot"
    ;;
  list)
    adb shell ls -lt "$SHOTS" | head -n 6
    ;;
  pull)
    dest="${1:-$ROOT/artifacts/demo/raw}"
    mkdir -p "$dest"
    newest="$(adb shell ls -t "$SHOTS" | tr -d '\r' | grep -E '\.mp4$' | head -n 1)"
    [[ -n "$newest" ]] || { echo "[record] no recording in $SHOTS" >&2; exit 1; }
    adb pull "$SHOTS/$newest" "$dest/" >/dev/null
    echo "[record] $dest/$newest"
    ;;
  screen)
    seconds="${1:-10}"; dest="${2:-$ROOT/artifacts/demo/raw}"
    mkdir -p "$dest"
    name="screen-$(date +%Y%m%d-%H%M%S).mp4"
    adb shell screenrecord --time-limit "$seconds" "/sdcard/Download/$name"
    adb pull "/sdcard/Download/$name" "$dest/" >/dev/null
    adb shell rm "/sdcard/Download/$name"
    echo "[record] $dest/$name (raw display buffer; check it with tools/demo/check-frame.sh)"
    ;;
  start|stop)
    cat <<'TXT'
[record] Start and stop the system recorder in the headset; adb cannot (see the header of this script).
  - Universal menu (Meta button) -> Camera -> Record video; the same panel stops it. A red dot shows while recording.
  - Controller shortcut: the recorder has one ("START_RECORDING_CONTROLLER_SHORTCUT" in its code); the chord is
    UNVERIFIED on v209, try it once off camera before relying on it.
  Then: tools/demo/record.sh pull && tools/demo/check-frame.sh artifacts/demo/raw/<file>.mp4
TXT
    ;;
  *)
    echo "unknown command $cmd" >&2; exit 2 ;;
esac
