#!/usr/bin/env bash
# Follow (or dump) Unity logcat lines for a tag.
# Usage: tools/device/logcat.sh [tag] [--dump|--clear]     default tag: NubiSpike
#   --dump prints the current buffer and exits; --clear drops old lines, then follows.
set -euo pipefail

TAG="${1:-NubiSpike}"
MODE="${2:-}"
# Quest's default 256 KiB main buffer rotates in about a minute; enlarge it so Unity lines survive.
adb logcat -G 16M
if [[ "$MODE" == "--clear" ]]; then adb logcat -c; MODE=""; fi
ARGS=(-v time -s Unity:V)
if [[ "$MODE" == "--dump" ]]; then ARGS=(-d "${ARGS[@]}"); fi
adb logcat "${ARGS[@]}" | grep --line-buffered -F "[$TAG]"
