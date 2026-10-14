#!/usr/bin/env bash
# Rulează Unity în batchmode pe proiectul ăsta.
# Folosire: tools/unity.sh compile|test|setup|build-mac|build-win|method <Namespace.Clasa.Metoda> [argumente Unity în plus]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd -P)"
if [ "$(uname)" = "Darwin" ]; then
  UNITY="${UNITY_EDITOR:-/Volumes/Dock & Hub 2TB/DevStorage/unity/editors/6000.3.26f1/Unity.app/Contents/MacOS/Unity}"
  # Pe Mac, cache-ul de pachete stă pe SSD, nu pe discul intern.
  export UPM_CACHE_ROOT="${UPM_CACHE_ROOT:-/Volumes/Dock & Hub 2TB/DevStorage/unity/upm-cache}"
else
  # HP Forge (Linux): editorul instalat de Unity CLI.
  UNITY="${UNITY_EDITOR:-$HOME/Unity/Hub/Editor/6000.3.26f1/Editor/Unity}"
  export UPM_CACHE_ROOT="${UPM_CACHE_ROOT:-$HOME/.cache/unity3d/upm}"
fi
mkdir -p "$ROOT/Logs" "$UPM_CACHE_ROOT"

cmd="${1:-compile}"
shift || true
base=(-batchmode -projectPath "$ROOT")

run() {
  local log="$1"; shift
  set +e
  "$UNITY" "${base[@]}" -logFile "$ROOT/Logs/$log" "$@"
  local code=$?
  set -e
  # Arată erorile de compilare direct în terminal.
  grep -E "error CS[0-9]+|Scripts have compiler errors|No valid Unity Editor license" "$ROOT/Logs/$log" | sort -u | head -40 || true
  echo "unity $cmd -> exit $code (log: Logs/$log)"
  return $code
}

case "$cmd" in
  compile)   run compile.log -nographics -quit "$@" ;;
  test)      run test.log -nographics -runTests -testPlatform EditMode -testResults "$ROOT/Logs/editmode-results.xml" "$@" ;;
  setup)     run setup.log -nographics -quit -executeMethod DarkestBasement.EditorTools.ProjectSetup.Run "$@" ;;
  build-mac) run build-mac.log -nographics -quit -executeMethod DarkestBasement.EditorTools.BuildScript.BuildMac "$@" ;;
  build-win) run build-win.log -nographics -quit -executeMethod DarkestBasement.EditorTools.BuildScript.BuildWindows "$@" ;;
  method)    m="$1"; shift; run method.log -quit -executeMethod "$m" "$@" ;;
  *) echo "comandă necunoscută: $cmd" >&2; exit 2 ;;
esac
