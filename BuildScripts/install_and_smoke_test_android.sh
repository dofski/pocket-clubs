#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APK_PATH="${POCKET_CLUBS_APK_PATH:-${PROJECT_ROOT}/Builds/Android/PocketClubs-Gate1-Debug.apk}"
PACKAGE_NAME="com.gptbuilder.pocketclubs"
MAIN_ACTIVITY="com.unity3d.player.UnityPlayerActivity"
mkdir -p "${PROJECT_ROOT}/Logs"

if ! command -v adb >/dev/null 2>&1; then
  echo "[BLOCKED] adb is not installed in this environment." >&2
  exit 2
fi

if [[ ! -s "${APK_PATH}" ]]; then
  echo "[BLOCKED] APK artifact does not exist at ${APK_PATH}. Build the APK first." >&2
  exit 2
fi

DEVICE_SERIAL="$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
if [[ -z "${DEVICE_SERIAL}" ]]; then
  echo "[BLOCKED] No authorized Android device connected via ADB." >&2
  exit 2
fi

echo "Using authorized Android device: ${DEVICE_SERIAL}"
adb -s "${DEVICE_SERIAL}" logcat -c
adb -s "${DEVICE_SERIAL}" install -r "${APK_PATH}"
adb -s "${DEVICE_SERIAL}" shell am start -n "${PACKAGE_NAME}/${MAIN_ACTIVITY}"

sleep 5
adb -s "${DEVICE_SERIAL}" exec-out screencap -p > "${PROJECT_ROOT}/Logs/device_launch_smoke.png"

# Test Android suspend / resume lifecycle
adb -s "${DEVICE_SERIAL}" shell input keyevent KEYCODE_HOME
sleep 2
adb -s "${DEVICE_SERIAL}" shell am start -n "${PACKAGE_NAME}/${MAIN_ACTIVITY}"
sleep 3
adb -s "${DEVICE_SERIAL}" logcat -d -v time > "${PROJECT_ROOT}/Logs/device_smoke_logcat.txt"

echo "[PASS] Installed and smoke-tested ${PACKAGE_NAME} on ${DEVICE_SERIAL}."
echo "  Screenshot : ${PROJECT_ROOT}/Logs/device_launch_smoke.png"
echo "  Logcat     : ${PROJECT_ROOT}/Logs/device_smoke_logcat.txt"
