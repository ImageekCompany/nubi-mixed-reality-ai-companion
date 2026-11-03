#!/usr/bin/env bash
# Static quality gates of docs/definition-of-done.md §2. They need no editor, so CI runs them on every push
# (.github/workflows/gates.yml); tools/ci/test.sh runs them first. Exits non-zero on any hit.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$ROOT/JollyXR"
SRC="$PROJECT/Assets/Jolly"
[[ -d "$SRC" ]] || { echo "[gates] no $SRC" >&2; exit 2; }

failures=0
check_absent() {
  local what="$1"; shift
  local hits
  if hits="$("$@")" && [[ -n "$hits" ]]; then
    echo "[gates] FAIL: $what" >&2
    echo "$hits" >&2
    failures=$((failures + 1))
  fi
}

check_absent "CancellationTokenSource.CancelAfter (use CancelAfterSlim / TimeoutController)" \
  grep -rnE "\.CancelAfter\(" "$SRC" --include=*.cs
check_absent "ToUniTask(cancelImmediately: true) aborts off the PlayerLoop" \
  grep -rnE "cancelImmediately: *true" "$SRC" --include=*.cs
check_absent "async void" \
  grep -rnE "async +void" "$SRC" --include=*.cs
# Stream declares Task-returning async methods, so the two Stream subclasses that override them are the only exceptions.
check_absent "System.Threading.Tasks.Task in Jolly code (use UniTask)" \
  grep -rnE "System\.Threading\.Tasks|(^|[^A-Za-z0-9_.])Task(<|\.|[[:space:]])" "$SRC" --include=*.cs \
    --exclude=PrefixedStream.cs --exclude=WebSocketUpgradeTests.cs
check_absent "scene search, Resources.Load or a singleton Instance (ADR-0009)" \
  grep -rnE "FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|GameObject\.Find|Resources\.Load|static[^=;(]*[[:space:]]Instance[[:space:]]*(\{|=>|=|;)" "$SRC" --include=*.cs
check_absent "JOLLY_DEBUG_CAPTURE / JOLLY_DEBUG_LOG in committed scripting defines" \
  grep -nE "JOLLY_DEBUG_(CAPTURE|LOG)" "$PROJECT/ProjectSettings/ProjectSettings.asset"

if [[ $failures -gt 0 ]]; then
  echo "[gates] $failures gate(s) failed" >&2
  exit 1
fi
echo "[gates] OK"
