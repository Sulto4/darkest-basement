#!/usr/bin/env bash
# Rulează Unity în batchmode pe proiectul ăsta.
# Folosire: tools/unity.sh compile|test|setup|build-mac|build-win|build-linux|method <Namespace.Clasa.Metoda> [argumente Unity în plus]
#           tools/unity.sh qa [tour|full|combat|sandbox|input] [seed] [argumente pentru player]   (doar Linux, cu Xvfb)
# Exemple QA: tools/unity.sh qa tour 12345
#             tools/unity.sh qa combat 7 -capture-fps 30
#             tools/unity.sh qa sandbox 7 -combat-sandbox monstro
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

# QA automat: pornește build-ul Linux sub Xvfb (ecran virtual 1920×1080) cu "-qa"; jocul se joacă singur,
# salvează capturi + qa-log.txt în Logs/qa/<script>/ și iese cu 0 (curat) sau 1 (erori/blocaj).
qa() {
  local script="${1:-tour}"; shift || true
  local seed="12345"
  if [ $# -gt 0 ] && [[ "$1" =~ ^-?[0-9]+$ ]]; then seed="$1"; shift; fi
  local player="$ROOT/Builds/linux/DarkestBasement.x86_64"
  if [ ! -x "$player" ]; then echo "lipsește $player: rulează întâi tools/unity.sh build-linux" >&2; return 2; fi
  if ! command -v xvfb-run >/dev/null; then echo "lipsește xvfb-run (QA merge doar pe Linux, pe Forge)" >&2; return 2; fi
  local out="$ROOT/Logs/qa/$script"
  rm -rf "$out"; mkdir -p "$out"
  local extra=("$@")
  if [ "$script" = "sandbox" ] && [[ " ${extra[*]-} " != *" -combat-sandbox"* ]]; then extra+=(-combat-sandbox); fi
  set +e
  # -force-glcore: sub Xvfb nu există placă video; Mesa (llvmpipe) desenează OpenGL în software.
  xvfb-run -a -s "-screen 0 1920x1080x24" "$player" \
    -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -force-glcore \
    -logFile "$out/player.log" -qa "$out" -qa-script "$script" -seed "$seed" "${extra[@]+"${extra[@]}"}"
  local code=$?
  set -e
  [ -f "$out/qa-summary.txt" ] && cat "$out/qa-summary.txt"
  echo "qa $script -> exit $code ($(ls "$out"/*.png 2>/dev/null | wc -l) capturi în Logs/qa/$script)"
  return $code
}

case "$cmd" in
  compile)     run compile.log -nographics -quit "$@" ;;
  test)        run test.log -nographics -runTests -testPlatform EditMode -testResults "$ROOT/Logs/editmode-results.xml" "$@" ;;
  setup)       run setup.log -nographics -quit -executeMethod DarkestBasement.EditorTools.ProjectSetup.Run "$@" ;;
  build-mac)   run build-mac.log -nographics -quit -executeMethod DarkestBasement.EditorTools.BuildScript.BuildMac "$@" ;;
  build-win)   run build-win.log -nographics -quit -executeMethod DarkestBasement.EditorTools.BuildScript.BuildWindows "$@" ;;
  build-linux) run build-linux.log -nographics -quit -executeMethod DarkestBasement.EditorTools.BuildScript.BuildLinux "$@" ;;
  qa)          qa "$@" ;;
  method)      m="$1"; shift; run method.log -quit -executeMethod "$m" "$@" ;;
  *) echo "comandă necunoscută: $cmd" >&2; exit 2 ;;
esac
