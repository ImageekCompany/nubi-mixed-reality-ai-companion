#!/usr/bin/env bash
# Install the APK, grant runtime permissions over adb, and (re)launch the app.
# Usage: tools/device/install-run.sh [app.apk]          (ANDROID_SERIAL selects the device)
#        tools/device/install-run.sh --restore-headset   undo the NUBI_HEADLESS overrides
#   NUBI_PACKAGE=com.nubi.companion
#   NUBI_SKIP_INSTALL=1   relaunch only
#   NUBI_HEADLESS=1       nobody is wearing the headset: wake it, fake proximity, pause Guardian
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PKG="${NUBI_PACKAGE:-com.nubi.companion}"

if [[ "${1:-}" == "--restore-headset" ]]; then
  adb shell am broadcast -a com.oculus.vrpowermanager.automation_disable >/dev/null
  adb shell setprop debug.oculus.guardian_pause 0
  echo "proximity sensor and Guardian restored"
  exit 0
fi

APK="${1:-$ROOT/JollyXR/Builds/Android/Nubi.apk}"
PERMISSIONS=(
  horizonos.permission.HEADSET_CAMERA
  android.permission.RECORD_AUDIO
  com.oculus.permission.USE_SCENE
)

# The default 256 KiB main log buffer on Quest rotates in about a minute; Unity lines get lost.
adb logcat -G 16M

if [[ "${NUBI_SKIP_INSTALL:-0}" != "1" ]]; then
  # -g grants every runtime permission in the manifest, so no system dialog blocks a headless run.
  adb install -r -g "$APK"
fi
for p in "${PERMISSIONS[@]}"; do
  adb shell pm grant "$PKG" "$p" 2>/dev/null && echo "granted $p" || echo "pm grant failed for $p (needs the in-app dialog)"
done

if [[ "${NUBI_HEADLESS:-0}" == "1" ]]; then
  adb shell input keyevent KEYCODE_WAKEUP
  adb shell am broadcast -a com.oculus.vrpowermanager.prox_close >/dev/null
  # Without a person in the headset the Guardian setup dialog blocks app launches ("Launch is blocked because:
  # a Guardian dialog is currently showing"). Developer-mode property; restore with --restore-headset.
  adb shell setprop debug.oculus.guardian_pause 1
fi

ACTIVITY="$(adb shell cmd package resolve-activity --brief "$PKG" | tail -n 1 | tr -d '\r')"
adb shell am force-stop "$PKG"
adb shell am start -n "$ACTIVITY" >/dev/null
echo "launched $ACTIVITY"
