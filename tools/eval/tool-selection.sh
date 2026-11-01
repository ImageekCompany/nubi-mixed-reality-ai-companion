#!/usr/bin/env bash
# Live tool-selection eval (docs/perf/tool-selection-eval.md): the PlayMode test Jolly.Tests.PlayMode.Eval.ToolSelectionEval,
# which tools/ci/test.sh never runs ([Explicit]). Sends ~20 text turns to the Meta Responses API with the app's prompt
# and tools; reads META_API_KEY from the repository .env and never prints it. About $0.01-0.02 per run: log it in
# docs/api-spend.md.
# Usage: tools/eval/tool-selection.sh [minimal|low] [tags]   (default minimal, every case)
#   tags: comma-separated case tags (an expected tool name, greeting, small_talk), e.g. greeting,come_here,find_object
#   UNITY=/path/to/Unity   override the editor (default: Hub 6000.6.6f1)
# Writes artifacts/tool-selection-<effort>.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity}"
EFFORT="${1:-minimal}"
OUT="$ROOT/artifacts"
mkdir -p "$OUT"
"$ROOT/tools/ci/restore-samples.sh"

export JOLLY_EVAL_EFFORT="$EFFORT"
export JOLLY_EVAL_TAGS="${2:-}"
export JOLLY_EVAL_COMMIT="$(git -C "$ROOT" rev-parse --short HEAD)$(git -C "$ROOT" diff --quiet -- JollyXR/Assets/Jolly/Runtime || echo '+dirty')"
export JOLLY_EVAL_OUT="$OUT/tool-selection-$EFFORT.md"
rm -f "$JOLLY_EVAL_OUT"

set +e
"$UNITY" -batchmode -nographics -projectPath "$ROOT/JollyXR" -runTests -testPlatform PlayMode \
  -testFilter "Jolly.Tests.PlayMode.Eval.ToolSelectionEval" -testCategory LiveEval \
  -testResults "$OUT/tool-selection-$EFFORT.xml" -logFile "$OUT/tool-selection-$EFFORT.log"
code=$?
set -e

grep -h "\[ToolSelectionEval\]" "$OUT/tool-selection-$EFFORT.log" | sort -u || true
if [[ ! -s "$JOLLY_EVAL_OUT" ]]; then
  echo "[eval] no report (exit=$code, log: $OUT/tool-selection-$EFFORT.log)" >&2
  exit 1
fi
cat "$JOLLY_EVAL_OUT"
exit $code
