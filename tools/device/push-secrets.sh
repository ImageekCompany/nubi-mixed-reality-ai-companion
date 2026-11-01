#!/usr/bin/env bash
# Push the device subset of .env to the app's external files dir as secrets.json.
# The app reads Application.persistentDataPath/secrets.json; nothing secret is baked into the APK.
# Usage: tools/device/push-secrets.sh            (ANDROID_SERIAL selects the device, as for adb)
#   ENV_FILE=path/.env   NUBI_PACKAGE=com.nubi.companion   TMPDIR=where the temp JSON is written
# Only META_API_KEY, WIT_AI_CLIENT_TOKEN and WIT_AI_APP_ID are pushed; the Wit *server* token stays on the dev machine.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ENV_FILE="${ENV_FILE:-$ROOT/.env}"
PKG="${NUBI_PACKAGE:-com.nubi.companion}"
DEST="/sdcard/Android/data/$PKG/files"

[[ -f "$ENV_FILE" ]] || { echo "missing $ENV_FILE" >&2; exit 1; }
TMP="$(mktemp "${TMPDIR:-/tmp}/nubi-secrets.XXXXXX")"
trap 'rm -f "$TMP"' EXIT

python3 -I - "$ENV_FILE" "$TMP" <<'PY'
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
with open(sys.argv[2], "w", encoding="utf-8") as f:
    json.dump(values, f)
missing = [k for k in wanted if k not in values]
print("secrets.json keys:", ", ".join(k for k in wanted if k in values) or "(none)")
if missing:
    print("missing in .env:", ", ".join(missing), file=sys.stderr)
PY

adb shell mkdir -p "$DEST"
adb push "$TMP" "$DEST/secrets.json" >/dev/null
echo "pushed $DEST/secrets.json"
