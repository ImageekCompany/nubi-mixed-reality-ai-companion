#!/usr/bin/env bash
# Configure the Unity project for Quest and build an APK in batchmode.
# Usage: tools/device/build.sh [--character <id>] [--embed-secrets] [output.apk]
#   --character <id>                   bake this character in as the default; a local, git-ignored one ships in the
#                                      APK only when named here (docs/characters.md)
#   --embed-secrets                    put META_API_KEY, WIT_AI_CLIENT_TOKEN and WIT_AI_APP_ID from ENV_FILE (default
#                                      .env) into the APK, default output Nubi-keyed.apk (ADR-0049). Anyone holding that
#                                      APK can extract and spend the key: never publish it, revoke the key afterwards.
#   UNITY=/path/to/Unity               override the editor (default: Hub 6000.6.6f1)
#   NUBI_PRE_BUILD_METHOD=Ns.Class.M   extra -executeMethod run between setup and build (e.g. a scene generator)
#   NUBI_DEVELOPMENT=1                 development build
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$ROOT/JollyXR"
CHARACTER_ARGS=()
EMBED_SECRETS=0
POSITIONAL=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --character)
      [[ $# -ge 2 && -n "$2" ]] || { echo "[build] --character needs an id" >&2; exit 2; }
      CHARACTER_ARGS=(-nubiCharacter "$2"); shift 2 ;;
    --embed-secrets) EMBED_SECRETS=1; shift ;;
    *) POSITIONAL+=("$1"); shift ;;
  esac
done
DEFAULT_APK="Nubi.apk"
if [[ "$EMBED_SECRETS" == "1" ]]; then DEFAULT_APK="Nubi-keyed.apk"; fi
OUT="${POSITIONAL[0]:-$PROJECT/Builds/Android/$DEFAULT_APK}"
# Unity would resolve a relative path against the project, not the caller's directory.
case "$OUT" in /*) ;; *) OUT="$PWD/$OUT" ;; esac
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
if [[ "$EMBED_SECRETS" == "1" ]]; then
  SECRETS_FILE="$(mktemp "${TMPDIR:-/tmp}/nubi-embed.XXXXXX")"
  trap 'rm -f "$SECRETS_FILE"' EXIT
  python3 -I - "${ENV_FILE:-$ROOT/.env}" "$SECRETS_FILE" <<'PY'
import json, sys
wanted = ["META_API_KEY", "WIT_AI_CLIENT_TOKEN", "WIT_AI_APP_ID"]
values = {}
for raw in open(sys.argv[1], encoding="utf-8"):
    line = raw.strip()
    if not line or line.startswith("#") or "=" not in line:
        continue
    key, _, value = line.partition("=")
    key = key.strip().removeprefix("export ").strip()
    value = value.strip().strip('"').strip("'")
    if key in wanted and value:
        values[key] = value
missing = [k for k in wanted if k not in values]
if missing:
    sys.exit("missing in the env file: " + ", ".join(missing))
with open(sys.argv[2], "w", encoding="utf-8") as f:
    json.dump(values, f)
PY
  export NUBI_EMBED_SECRETS_FILE="$SECRETS_FILE"
fi
DEV_ARGS=()
if [[ "${NUBI_DEVELOPMENT:-0}" == "1" ]]; then DEV_ARGS=(-nubiDevelopment); fi
run_unity build -executeMethod Jolly.Editor.Build.QuestBuild.BuildApk -nubiOut "$OUT" ${DEV_ARGS[@]+"${DEV_ARGS[@]}"} \
  ${CHARACTER_ARGS[@]+"${CHARACTER_ARGS[@]}"}

grep -E "\[QuestBuild\] Result=|\[CharacterSceneProcessor\]" "$LOGS/build.log" || true
if [[ "$EMBED_SECRETS" == "1" ]]; then echo "[build] KEYED APK: share privately only; revoke the key when testing ends"; fi
ls -lh "$OUT"
