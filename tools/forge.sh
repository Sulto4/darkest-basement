#!/usr/bin/env bash
# Rulează Unity pe HP Forge (Linux, 32 GB RAM) și aduce înapoi rezultatele.
# Mac-ul rămâne sursa de adevăr pentru cod și artă; Forge doar compilează, testează și face build-uri.
#
# Folosire: tools/forge.sh compile|test|setup|build-win|build-mac|method <Clasa.Metoda> [argumente Unity]
#           tools/forge.sh push            # doar sincronizează
#
# Forge găzduiește și producția Voost Control, așa că Unity rulează plafonat (memorie, CPU, timp)
# și serializat (un singur Unity odată, prin flock).
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
  # .meta-urile generate de Unity pe Forge sunt protejate de --delete până le aducem pe Mac.
  rsync -a --delete \
    --filter='P *.meta' --filter='P /Library/' --filter='P /Logs/' --filter='P /Builds/' --filter='P /Temp/' \
    --filter='P /UserSettings/' --filter='P /Assets/_Game/Scenes/' \
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
  mkdir -p "$ROOT/Logs"
  rsync -a "$HOST:$RDIR/Logs/" "$ROOT/Logs/" 2>/dev/null || true
}

push
[ "$cmd" = "push" ] && exit 0

set +e
ssh "$HOST" "cd '$RDIR' && flock /tmp/darkest-basement-unity.lock \
  systemd-run --user --scope --quiet -p MemoryMax=8G -p MemorySwapMax=0 -p CPUQuota=600% -p RuntimeMaxSec=2400 \
  nice -n 10 env UNITY_NO_CONSENT_PROMPT=1 tools/unity.sh $cmd $*"
code=$?
set -e

pull
if [[ "$cmd" == build-* ]]; then
  mkdir -p "$ROOT/Builds"
  rsync -a "$HOST:$RDIR/Builds/" "$ROOT/Builds/"
fi
echo "forge $cmd -> exit $code"
exit $code
