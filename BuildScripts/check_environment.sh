#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
echo "=== Pocket Clubs Toolchain & Environment Check ==="
echo "Project Root : ${PROJECT_ROOT}"
echo "OS / Kernel  : $(uname -srm)"

resolve_unity_editor() {
  if [[ -n "${UNITY_EDITOR_PATH:-}" ]] && [[ -x "${UNITY_EDITOR_PATH}" ]]; then
    echo "${UNITY_EDITOR_PATH}"
    return 0
  fi

  local candidates=(
    "/opt/unity/Editor/Unity"
    "${HOME}/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
    "/Applications/Unity/Hub/Editor/6000.0.24f1/Unity.app/Contents/MacOS/Unity"
  )
  for c in "${candidates[@]}"; do
    if [[ -x "${c}" ]]; then
      echo "${c}"
      return 0
    fi
  done

  if command -v unity-editor >/dev/null 2>&1; then
    command -v unity-editor
    return 0
  fi
  return 1
}

STATUS_BLOCKED=0

if UNITY_BIN="$(resolve_unity_editor)"; then
  echo "[PASS] Unity Editor binary found: ${UNITY_BIN}"
else
  echo "[BLOCKED] Unity Editor (6000.0.24f1 LTS) not found. Set UNITY_EDITOR_PATH or install via Unity Hub."
  STATUS_BLOCKED=1
fi

if [[ -n "${ANDROID_SDK_ROOT:-}" && -d "${ANDROID_SDK_ROOT}" ]] || [[ -n "${ANDROID_HOME:-}" && -d "${ANDROID_HOME}" ]]; then
  echo "[PASS] Android SDK directory configured."
else
  echo "[BLOCKED] ANDROID_SDK_ROOT / ANDROID_HOME not configured."
  STATUS_BLOCKED=1
fi

if [[ -n "${ANDROID_NDK_ROOT:-}" && -d "${ANDROID_NDK_ROOT}" ]]; then
  echo "[PASS] Android NDK directory configured."
else
  echo "[BLOCKED] ANDROID_NDK_ROOT not configured."
  STATUS_BLOCKED=1
fi

if command -v java >/dev/null 2>&1; then
  echo "[PASS] Java runtime found: $(java -version 2>&1 | head -n 1)"
else
  echo "[BLOCKED] JDK (OpenJDK 17 recommended for Unity 6 Android) not found."
  STATUS_BLOCKED=1
fi

if command -v adb >/dev/null 2>&1; then
  echo "[PASS] ADB binary found: $(command -v adb)"
  adb devices -l || true
else
  echo "[BLOCKED] Android Platform Tools (adb) not found."
  STATUS_BLOCKED=1
fi

exit "${STATUS_BLOCKED}"
