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
  echo "Set UNITY_EDITOR_PATH=/path/to/Unity and re-run ${BASH_SOURCE[0]}." >&2
  exit 2
fi

LOG_FILE="${PROJECT_ROOT}/Logs/bootstrap_assets.log"
echo "Running PocketClubs.Editor.ProjectBootstrap.GenerateAllFromCommandLine..."
"${UNITY_BIN}" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "${PROJECT_ROOT}" \
  -executeMethod PocketClubs.Editor.ProjectBootstrap.GenerateAllFromCommandLine \
  -logFile "${LOG_FILE}"

echo "[PASS] Generated Gate 0 & Gate 1 assets and scenes. Log: ${LOG_FILE}"
