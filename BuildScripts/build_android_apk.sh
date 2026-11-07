#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "${PROJECT_ROOT}/Logs" "${PROJECT_ROOT}/Builds/Android"

UNITY_BIN="${UNITY_EDITOR_PATH:-}"
if [[ -z "${UNITY_BIN}" ]]; then
  if [[ -x "${HOME}/Unity/Hub/Editor/6000.0.24f1/Editor/Unity" ]]; then
    UNITY_BIN="${HOME}/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
  elif [[ -x "/Applications/Unity/Hub/Editor/6000.0.24f1/Unity.app/Contents/MacOS/Unity" ]]; then
    UNITY_BIN="/Applications/Unity/Hub/Editor/6000.0.24f1/Unity.app/Contents/MacOS/Unity"
  fi
fi

if [[ -z "${UNITY_BIN}" ]] || [[ ! -x "${UNITY_BIN}" ]]; then
  echo "[BLOCKED] Unity Editor 6000.0.24f1 with Android Build Support is not installed." >&2
  echo "Set UNITY_EDITOR_PATH=/path/to/Unity and ensure Android SDK/NDK/JDK are installed." >&2
  exit 2
fi

APK_PATH="${POCKET_CLUBS_APK_PATH:-${PROJECT_ROOT}/Builds/Android/PocketClubs-Gate1-Debug.apk}"
export POCKET_CLUBS_APK_PATH="${APK_PATH}"
BUILD_LOG="${PROJECT_ROOT}/Logs/android_apk_build.log"

echo "=== Building Android Development APK ==="
"${UNITY_BIN}" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "${PROJECT_ROOT}" \
  -buildTarget Android \
  -executeMethod PocketClubs.Editor.AndroidBuildPipeline.BuildDebugApkFromCommandLine \
  -logFile "${BUILD_LOG}"

if [[ ! -s "${APK_PATH}" ]]; then
  echo "[FAIL] Expected APK artifact missing or empty: ${APK_PATH}" >&2
  exit 1
fi

echo "[PASS] APK built: ${APK_PATH}"
ls -lh "${APK_PATH}"
if command -v sha256sum >/dev/null 2>&1; then
  sha256sum "${APK_PATH}"
elif command -v shasum >/dev/null 2>&1; then
  shasum -a 256 "${APK_PATH}"
fi
