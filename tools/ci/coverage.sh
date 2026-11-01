#!/usr/bin/env bash
# Line-coverage summary and gate for the Jolly.* runtime assemblies (definition-of-done.md §1, item 1.1).
# Usage: tools/ci/coverage.sh [Summary.xml]   (default: artifacts/coverage/Report/Summary.xml)
#   COVERAGE_GATE=0   print the summary without enforcing the threshold (a single-platform run)
# tools/ci/test.sh runs the tests with com.unity.testtools.codecoverage and calls this afterwards.
# Writes artifacts/coverage-summary.txt. Exits non-zero below the threshold or without a summary.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SUMMARY="${1:-$ROOT/artifacts/coverage/Report/Summary.xml}"
OUT="$ROOT/artifacts/coverage-summary.txt"

# The DoD's "Core, Net, Providers and Audio": the assemblies EditMode must cover. The view, XR and
# composition assemblies are reported but not gated; their logic is exercised by PlayMode scene tests.
PURE_LOGIC=(Jolly.Core Jolly.Net Jolly.Providers.MetaApi Jolly.Providers.Wit Jolly.Audio)
# Measured EditMode + PlayMode value minus 2 points; raise it with the measurement, never lower it
# to make a run pass. History in docs/release-checklist.md item 1.1.
THRESHOLD=91.6

if [[ ! -s "$SUMMARY" ]]; then
  echo "[coverage] FAIL: no summary at $SUMMARY" >&2
  exit 1
fi

attr() { xmllint --xpath "string($1)" "$SUMMARY" 2>/dev/null || echo ""; }
pct() { awk -v c="$1" -v t="$2" 'BEGIN { if (t > 0) printf "%.1f", 100 * c / t; else printf "n/a" }'; }

row() { printf "%-28s %8s %8s %7s%s\n" "$@"; }
report="$(row assembly covered coverable line% "")"
covered=0; coverable=0
for name in $(xmllint --xpath "//Assembly/@name" "$SUMMARY" | grep -oE 'name="[^"]*"' | cut -d'"' -f2); do
  c="$(attr "//Assembly[@name='$name']/@coveredlines")"
  t="$(attr "//Assembly[@name='$name']/@coverablelines")"
  mark=""
  for p in "${PURE_LOGIC[@]}"; do
    if [[ "$p" == "$name" ]]; then
      mark=" *"; covered=$((covered + c)); coverable=$((coverable + t))
    fi
  done
  report+=$'\n'"$(row "$name" "$c" "$t" "$(pct "$c" "$t")" "$mark")"
done
total="$(pct "$covered" "$coverable")"
report+=$'\n'"$(row "all Jolly.* runtime" "$(attr "//Summary/Coveredlines")" "$(attr "//Summary/Coverablelines")" "$(attr "//Summary/Linecoverage")" "")"
report+=$'\n'"$(row "pure logic (*)" "$covered" "$coverable" "$total" "  (threshold $THRESHOLD)")"
echo "$report" | tee "$OUT"

for p in "${PURE_LOGIC[@]}"; do
  if [[ -z "$(attr "//Assembly[@name='$p']/@name")" ]]; then
    echo "[coverage] FAIL: $p missing from the report (assembly filter or a failed run)" >&2
    exit 1
  fi
done

if [[ "${COVERAGE_GATE:-1}" == "0" ]]; then
  echo "[coverage] pure logic $total% (gate skipped)"
elif awk -v v="$total" -v t="$THRESHOLD" 'BEGIN { exit !(v < t) }'; then
  echo "[coverage] FAIL: pure logic $total% < $THRESHOLD%" >&2
  exit 1
else
  echo "[coverage] pure logic $total% >= $THRESHOLD%"
fi
