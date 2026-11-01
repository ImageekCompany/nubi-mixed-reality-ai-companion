#!/usr/bin/env bash
# Reset the app between demo takes (docs/demo-script.md "Reset between takes"): force-stop, clear the conversation and
# scene memory, relaunch, and optionally wait for the launch warm-up.
# Usage: tools/demo/reset.sh [--warmup] [--keep-memory]
#   --warmup       wait (up to 40 s) for evt=warmup.done, then print the selected character. The warm-up opens the ASR,
#                  TTS and camera paths; the throwaway greeting turn still needs the wearer (demo-script.md).
#   --keep-memory  relaunch only
#   ANDROID_SERIAL selects the device, NUBI_PACKAGE=com.nubi.companion.
# Caption size and persona live in PlayerPrefs and survive; usage.json (the spend cap) is never touched.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PKG="${NUBI_PACKAGE:-com.nubi.companion}"
WARMUP=0
KEEP=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --warmup) WARMUP=1 ;;
    --keep-memory) KEEP=1 ;;
    *) echo "unknown argument $1" >&2; exit 2 ;;
  esac
  shift
done

adb get-state >/dev/null || { echo "[reset] no device" >&2; exit 2; }
adb shell am force-stop "$PKG"
if [[ $KEEP -eq 0 ]]; then
  adb shell rm -rf "/sdcard/Android/data/$PKG/files/memory"
  echo "[reset] memory cleared"
fi
adb logcat -c
NUBI_SKIP_INSTALL=1 NUBI_PACKAGE="$PKG" "$ROOT/tools/device/install-run.sh" >/dev/null
echo "[reset] relaunched $PKG"

if [[ $WARMUP -eq 1 ]]; then
  for _ in $(seq 1 40); do
    # Not grep -q: it exits at the first match, adb takes SIGPIPE and pipefail turns the match into a miss.
    if adb logcat -d -s Unity:I 2>/dev/null | grep "evt=warmup.done" >/dev/null; then
      adb logcat -d -s Unity:I | grep -oE "evt=(character.selected|warmup.done)[^\"]*" | sort -u | sed 's/^/[reset] /'
      echo "[reset] warm; say the throwaway greeting off camera, then Settings -> Clear memory (a relaunch would make the next turn cold again)"
      exit 0
    fi
    sleep 1
  done
  echo "[reset] no evt=warmup.done within 40 s (headset asleep? check: adb logcat -s Unity)" >&2
  exit 1
fi
