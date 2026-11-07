#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p "${PROJECT_ROOT}/Logs"

UNITY_BIN="${UNITY_EDITOR_PATH:-}"
if [[ -z "${UNITY_BIN}" ]]; then
  if [[ -x "${HOME}/Unity/Hub/Editor/6000.0.24f1/Editor/Unity" ]]; then
    UNITY_BIN="${HOME}/Unity/Hub/Editor/6000.0.24f1/Editor/Unity"
  elif [[ -x "/Applications/Unity/Hub/Editor/6000.0.24f1/Unity.app/Contents/MacOS/Unity" ]]; then
    UNITY_BIN="/Applications/Unity/Hub/Editor/6000.0.24f1/Unity.app/Contents/MacOS/Unity"
  fi
fi

if [[ -z "${UNITY_BIN}" ]] || [[ ! -x "${UNITY_BIN}" ]]; then
  echo "[BLOCKED] Unity Editor 6000.0.24f1 is not installed in this environment." >&2
  echo "Cannot execute Unity Test Framework EditMode/PlayMode suites without Unity Editor." >&2
  exit 2
fi

EDITMODE_XML="${PROJECT_ROOT}/Logs/TestResults-EditMode.xml"
EDITMODE_LOG="${PROJECT_ROOT}/Logs/TestRun-EditMode.log"
PLAYMODE_XML="${PROJECT_ROOT}/Logs/TestResults-PlayMode.xml"
PLAYMODE_LOG="${PROJECT_ROOT}/Logs/TestRun-PlayMode.log"

echo "=== Running EditMode Tests ==="
"${UNITY_BIN}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_ROOT}" \
  -runTests \
  -testPlatform EditMode \
  -testResults "${EDITMODE_XML}" \
  -logFile "${EDITMODE_LOG}"

echo "=== Running PlayMode Tests ==="
"${UNITY_BIN}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_ROOT}" \
  -runTests \
  -testPlatform PlayMode \
  -testResults "${PLAYMODE_XML}" \
  -logFile "${PLAYMODE_LOG}"

echo "[PASS] EditMode & PlayMode tests finished."
echo "  EditMode XML: ${EDITMODE_XML}"
echo "  PlayMode XML: ${PLAYMODE_XML}"
