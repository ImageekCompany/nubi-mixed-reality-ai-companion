#!/usr/bin/env bash
# Batchmode IL2CPP release APK via Jolly.Editor.Build.QuestBuild.BuildApk (docs/definition-of-done.md §3).
# Usage: tools/ci/build.sh [output.apk]   (default: artifacts/Nubi.apk)
#   UNITY=/path/to/Unity    override the editor (default: Hub 6000.6.6f1)
#   NUBI_DEVELOPMENT=1      development build
# For a fresh checkout whose Active Input Handling is not yet applied, run tools/device/build.sh once instead:
# that setting only takes effect after an editor restart.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$ROOT/JollyXR"
OUT_DIR="$ROOT/artifacts"
APK="${1:-$OUT_DIR/Nubi.apk}"
# Unity would resolve a relative path against the project, not the caller's directory.
case "$APK" in /*) ;; *) APK="$PWD/$APK" ;; esac
LOG="$OUT_DIR/build.log"
mkdir -p "$OUT_DIR" "$(dirname "$APK")"
rm -f "$APK"
"$ROOT/tools/ci/restore-samples.sh"

DEV_ARGS=()
if [[ "${NUBI_DEVELOPMENT:-0}" == "1" ]]; then DEV_ARGS=(-nubiDevelopment); fi

echo "[build] $APK (log: $LOG)"
set +e
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -buildTarget Android \
  -executeMethod Jolly.Editor.Build.QuestBuild.BuildApk -nubiOut "$APK" ${DEV_ARGS[@]+"${DEV_ARGS[@]}"} \
  -logFile "$LOG"
code=$?
set -e

grep -E "\[QuestBuild\] Result=" "$LOG" || true
failed=0
if [[ $code -ne 0 ]]; then echo "[build] FAIL: editor exit=$code" >&2; failed=1; fi
if grep -hE "Assets/Jolly/.*: (warning|error) CS[0-9]{4}" "$LOG" | sort -u >&2; then
  echo "[build] FAIL: compiler diagnostics in Assets/Jolly" >&2; failed=1
fi
if [[ ! -s "$APK" ]]; then echo "[build] FAIL: no APK at $APK" >&2; failed=1; fi
if [[ $failed -ne 0 ]]; then
  tail -n 60 "$LOG" >&2
  exit 1
fi
ls -lh "$APK"
echo "[build] OK"
