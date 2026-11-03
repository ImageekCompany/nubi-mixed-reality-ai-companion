#!/usr/bin/env bash
# Batchmode EditMode + PlayMode tests with code coverage, and the static quality gates of
# docs/definition-of-done.md §1-2.
# Usage: tools/ci/test.sh [EditMode|PlayMode]...   (default: both; the coverage gate needs both)
#   UNITY=/path/to/Unity   override the editor (default: Hub 6000.6.6f1)
# Results and logs go to artifacts/. Exits non-zero on any failed gate.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents/MacOS/Unity}"
PROJECT="$ROOT/JollyXR"
OUT="$ROOT/artifacts"
COVERAGE="$OUT/coverage"
# com.unity.testtools.codecoverage 1.3.0 filter syntax (Documentation~/CoverageBatchmode.md).
COVERAGE_ASSEMBLIES="+Jolly.*,-Jolly.Tests.*,-Jolly.Testing,-Jolly.Editor*"
if [[ $# -gt 0 ]]; then PLATFORMS=("$@"); else PLATFORMS=(EditMode PlayMode); fi
mkdir -p "$OUT"
"$ROOT/tools/ci/restore-samples.sh"
rm -rf "$COVERAGE"

failures=0
fail() { echo "[test] FAIL: $*" >&2; failures=$((failures + 1)); }

# Static gates first: they are cheap and need no editor.
"$ROOT/tools/ci/gates.sh" || fail "static gates"

# Shipped content and scene wiring (Editor/Tools/Validators): personas, tables, clips, JollyBootstrap bindings.
echo "[test] content validation (log: $OUT/content.log)"
if ! "$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
     -executeMethod Jolly.Editor.Tools.Validators.ContentValidator.RunBatch -logFile "$OUT/content.log"; then
  fail "content validation"
  grep -hE "\[ContentValidator\]|error CS" "$OUT/content.log" | sort -u >&2 || true
fi

attr() { xmllint --xpath "string(/test-run/@$1)" "$2" 2>/dev/null || echo ""; }

last="${PLATFORMS[${#PLATFORMS[@]}-1]}"
for platform in "${PLATFORMS[@]}"; do
  name="$(echo "$platform" | tr '[:upper:]' '[:lower:]')"
  # dontClear keeps the earlier platform's results; the last run's report merges every run under $COVERAGE.
  coverage_options="dontClear;assemblyFilters:$COVERAGE_ASSEMBLIES"
  [[ "$platform" == "$last" ]] && coverage_options="$coverage_options;generateHtmlReport"
  results="$OUT/$name.xml"
  log="$OUT/$name.log"
  rm -f "$results"
  echo "[test] $platform (log: $log)"
  set +e
  "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform "$platform" \
    -testResults "$results" -logFile "$log" \
    -debugCodeOptimization -enableCodeCoverage -coverageResultsPath "$COVERAGE" -coverageOptions "$coverage_options"
  code=$?
  set -e

  # Executer.cs: 0 ok, 2 tests failed, 3 run error (incl. compile errors), 4 platform not found.
  [[ $code -ne 0 ]] && fail "$platform exit=$code"
  if grep -hE "Assets/Jolly/.*: (warning|error) CS[0-9]{4}" "$log" | sort -u >&2; then
    fail "$platform: compiler diagnostics in Assets/Jolly"
  fi
  if [[ ! -s "$results" ]]; then
    fail "$platform: no results file"
    continue
  fi
  total="$(attr total "$results")"; passed="$(attr passed "$results")"; failed="$(attr failed "$results")"
  skipped="$(attr skipped "$results")"; inconclusive="$(attr inconclusive "$results")"
  echo "[test] $platform total=$total passed=$passed failed=$failed skipped=$skipped inconclusive=$inconclusive"
  [[ "${total:-0}" -gt 0 ]] || fail "$platform ran no tests"
  [[ "${failed:-1}" -eq 0 ]] || fail "$platform failed=$failed"
  [[ "${inconclusive:-0}" -eq 0 ]] || fail "$platform inconclusive=$inconclusive"
done

# Line coverage per assembly (HTML: artifacts/coverage/Report/index.htm), gated only on a full run.
gate=0
[[ " ${PLATFORMS[*]} " == *" EditMode "* && " ${PLATFORMS[*]} " == *" PlayMode "* ]] && gate=1
COVERAGE_GATE=$gate "$ROOT/tools/ci/coverage.sh" "$COVERAGE/Report/Summary.xml" || fail "coverage"

if [[ $failures -gt 0 ]]; then
  echo "[test] $failures gate(s) failed" >&2
  exit 1
fi
echo "[test] OK"
