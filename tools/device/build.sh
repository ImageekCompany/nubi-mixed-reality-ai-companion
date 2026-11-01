#!/usr/bin/env bash
# Configure the Unity project for Quest and build an APK in batchmode.
# Usage: tools/device/build.sh [--character <id>] [output.apk]
#   --character <id>                   bake this character in as the default; a local, git-ignored one ships in the
#                                      APK only when named here (docs/characters.md)
#   UNITY=/path/to/Unity               override the editor (default: Hub 6000.6.6f1)
#   NUBI_PRE_BUILD_METHOD=Ns.Class.M   extra -executeMethod run between setup and build (e.g. a scene generator)
#   NUBI_DEVELOPMENT=1                 development build
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$ROOT/JollyXR"
CHARACTER_ARGS=()
POSITIONAL=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --character)
      [[ $# -ge 2 && -n "$2" ]] || { echo "[build] --character needs an id" >&2; exit 2; }
      CHARACTER_ARGS=(-nubiCharacter "$2"); shift 2 ;;
    *) POSITIONAL+=("$1"); shift ;;
  esac
done
OUT="${POSITIONAL[0]:-$PROJECT/Builds/Android/Nubi.apk}"
LOGS="$PROJECT/Logs"
mkdir -p "$LOGS"
"$ROOT/tools/ci/restore-samples.sh"

run_unity() {
  local name="$1"; shift
  echo "[build] $name (log: $LOGS/$name.log)"
  if ! "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -buildTarget Android -logFile "$LOGS/$name.log" "$@"; then
    echo "[build] $name FAILED; tail of log:" >&2
    tail -n 60 "$LOGS/$name.log" >&2
    exit 1
  fi
}

# Separate editor sessions: setup changes Active Input Handling, which only takes effect after a restart.
run_unity setup -quit -executeMethod Jolly.Editor.Build.QuestProjectSetup.Apply
if [[ -n "${NUBI_PRE_BUILD_METHOD:-}" ]]; then
  run_unity prebuild -quit -executeMethod "$NUBI_PRE_BUILD_METHOD"
fi
DEV_ARGS=()
if [[ "${NUBI_DEVELOPMENT:-0}" == "1" ]]; then DEV_ARGS=(-nubiDevelopment); fi
run_unity build -executeMethod Jolly.Editor.Build.QuestBuild.BuildApk -nubiOut "$OUT" ${DEV_ARGS[@]+"${DEV_ARGS[@]}"} \
  ${CHARACTER_ARGS[@]+"${CHARACTER_ARGS[@]}"}

grep -E "\[QuestBuild\] Result=|\[CharacterSceneProcessor\]" "$LOGS/build.log" || true
ls -lh "$OUT"
