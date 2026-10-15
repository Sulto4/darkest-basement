#!/usr/bin/env bash
# Rulează Unity pe HP Forge (Linux, 32 GB RAM) și aduce înapoi rezultatele.
# Mac-ul rămâne sursa de adevăr pentru cod și artă; Forge doar compilează, testează și face build-uri.
#
# Folosire: tools/forge.sh compile|test|setup|build-win|build-mac|build-linux|method <Clasa.Metoda> [argumente Unity]
#           tools/forge.sh qa [tour|full|combat|sandbox] [seed] [argumente player]   # build Linux + joc automat sub Xvfb
#           tools/forge.sh push            # doar sincronizează
# Capturile QA ajung în Logs/qa/<script>/ (PNG, qa-log.txt, qa-summary.txt, player.log).
#
# Forge găzduiește și producția Voost Control, așa că Unity (și player-ul de QA) rulează plafonat
# (memorie, CPU, timp) și serializat (un singur Unity odată, prin flock).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd -P)"

# Mai mulți agenți pot chema scriptul deodată: îi punem la coadă (sincronizare + Unity, unul câte unul).
if [ -z "${FORGE_LOCKED:-}" ]; then
  export FORGE_LOCKED=1
  exec lockf -t 3600 /tmp/darkest-basement-forge.lock "$0" "$@"
fi
HOST="${FORGE_HOST:-hp-forge}"
RDIR="${FORGE_DIR:-/home/radu/Projects/darkest-basement}"
cmd="${1:-compile}"
shift || true

push() {
  ssh "$HOST" "mkdir -p '$RDIR'"
  # .meta-urile și asset-urile generate de Unity pe Forge sunt protejate de --delete până le aducem pe Mac.
  rsync -a --delete \
    --filter='P *.meta' --filter='P /Library/' --filter='P /Logs/' --filter='P /Builds/' --filter='P /Temp/' \
    --filter='P /UserSettings/' --filter='P /Assets/_Game/Scenes/' \
    --filter='P /Assets/_Game/Resources/Materials/' --filter='P /Assets/_Game/Resources/PostFx/' --filter='P /Assets/TextMesh Pro/' \
    --exclude='/Library/' --exclude='/Temp/' --exclude='/Logs/' --exclude='/Builds/' --exclude='/UserSettings/' \
    --exclude='/.git/' --exclude='/tools/sim/bin/' --exclude='/tools/sim/obj/' --exclude='/tools/sim/.dotnet-home/' \
    --exclude='/tools/audio/raw/' --exclude='__pycache__/' --exclude='.DS_Store' \
    "$ROOT/" "$HOST:$RDIR/"
}

pull() {
  # Doar .meta-urile noi (GUID-urile trebuie să fie aceleași peste tot, deci le comitem din Mac).
  rsync -a --ignore-existing --include='*/' --include='*.meta' --exclude='*' --prune-empty-dirs \
    "$HOST:$RDIR/Assets/" "$ROOT/Assets/"
  # Fișierele pe care le scrie Unity: scena generată, setările de proiect, lock-ul de pachete, logurile.
  rsync -a "$HOST:$RDIR/Assets/_Game/Scenes/" "$ROOT/Assets/_Game/Scenes/" 2>/dev/null || true
  rsync -a "$HOST:$RDIR/ProjectSettings/" "$ROOT/ProjectSettings/"
  rsync -a "$HOST:$RDIR/Packages/packages-lock.json" "$ROOT/Packages/" 2>/dev/null || true
  # Asset-urile generate de ProjectSetup (șabloane de materiale, profilul de post-procesare, TMP Essentials).
  mkdir -p "$ROOT/Assets/_Game/Resources"
  rsync -a "$HOST:$RDIR/Assets/_Game/Resources/Materials" "$ROOT/Assets/_Game/Resources/" 2>/dev/null || true
  rsync -a "$HOST:$RDIR/Assets/_Game/Resources/PostFx" "$ROOT/Assets/_Game/Resources/" 2>/dev/null || true
  # "TextMesh Pro" are spațiu în nume: îl aducem cu tar, o singură dată.
  if [ ! -d "$ROOT/Assets/TextMesh Pro" ]; then
    ssh "$HOST" "cd '$RDIR/Assets' && test -d 'TextMesh Pro' && tar -cf - 'TextMesh Pro' 'TextMesh Pro.meta'" 2>/dev/null \
      | tar -xf - -C "$ROOT/Assets" 2>/dev/null || true
  fi
  mkdir -p "$ROOT/Logs"
  rsync -a "$HOST:$RDIR/Logs/" "$ROOT/Logs/" 2>/dev/null || true
}

# Un pas pe Forge, în cușca de resurse: o singură instanță odată (flock), memorie/CPU/timp plafonate.
# RUNTIME = plafonul de timp al pasului: 2400 s pentru Unity; jocul automat "qa full" (un run întreg, ~45 min) primește 5400 s.
remote() {
  local runtime="${RUNTIME:-2400}"
  ssh "$HOST" "cd '$RDIR' && flock /tmp/darkest-basement-unity.lock \
    systemd-run --user --scope --quiet -p MemoryMax=8G -p MemorySwapMax=0 -p CPUQuota=600% -p RuntimeMaxSec=$runtime \
    nice -n 10 env UNITY_NO_CONSENT_PROMPT=1 tools/unity.sh $*"
}

push
[ "$cmd" = "push" ] && exit 0

set +e
if [ "$cmd" = "qa" ]; then
  # QA = build Linux proaspăt, apoi jocul automat sub Xvfb. Doi pași, fiecare cu plafonul lui.
  remote build-linux
  code=$?
  if [ $code -eq 0 ]; then
    RUNTIME=$([ "${1:-tour}" = "full" ] && echo 5400 || echo 2400) remote qa "$@"
    code=$?
  fi
else
  remote "$cmd" "$@"
  code=$?
fi
set -e

# Capturile QA vin într-un folder curat (altfel s-ar amesteca cu cele din rularea trecută).
if [ "$cmd" = "qa" ]; then rm -rf "$ROOT/Logs/qa/${1:-tour}"; fi
pull
if [ "$cmd" = "build-win" ] || [ "$cmd" = "build-mac" ]; then
  mkdir -p "$ROOT/Builds"
  rsync -a "$HOST:$RDIR/Builds/" "$ROOT/Builds/"
fi
echo "forge $cmd -> exit $code"
exit $code
