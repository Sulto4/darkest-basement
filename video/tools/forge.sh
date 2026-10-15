#!/bin/bash
# Tot ce e greu (Chrome, ffmpeg, mixaj) rulează pe HP Forge, nu pe Mac. Forge găzduiește și producția Voost Control, deci:
#  - fiecare pas greu ia ACELAȘI lock ca QA-ul Unity (/tmp/darkest-basement-unity.lock): randarea și QA-ul nu se suprapun;
#  - rulează într-un scope systemd plafonat (MemoryMax 6G, CPUQuota 500%, RuntimeMaxSec) cu nice 15;
#  - un singur job odată, în prim-plan (fără ssh în fundal, fără bucle de polling).
# Dacă lock-ul e ținut (QA în curs), comanda iese cu 75 după WAIT secunde: reîncearcă mai târziu, nu ocoli lock-ul.
#
#   bash tools/forge.sh sync                    → doar sincronizează video/ + out/assets pe Forge
#   bash tools/forge.sh snap <nume> <t1> <t2>…  → capturi 1920×1080 în out/snap/<nume>/   (sheet=4 pentru planșă)
#   bash tools/forge.sh sheet <nume> [k=2]      → k cadre din fiecare scenă + planșă      → out/snap/<nume>/sheet.jpg
#   bash tools/forge.sh render <scene…|all>     → segmente MP4 în out/seg/ pe Forge (o comandă < 9 min; împarte scenele pe apeluri)
#   bash tools/forge.sh mix                     → plan de sunet + mix.wav pe Forge, afișează balanța vocii
#   bash tools/forge.sh final <nume.mp4>        → lipește segmentele + mix.wav → out/<nume.mp4>, adus pe Mac
#   bash tools/forge.sh finish <nume.mp4>       → plan + mix (cu stem-uri) + lipire + mux, într-un singur pas cu lock
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd -P)"            # video/
HOST="${FORGE_HOST:-hp-forge}"
RDIR="${FORGE_VIDEO_DIR:-Projects/darkest-basement-video}"
WAIT="${WAIT:-60}"
LOCK="/tmp/darkest-basement-unity.lock"
cap() { # cap <RuntimeMaxSec> <comandă…>   (rulează pe Forge, în $RDIR)
  local rt="$1"; shift
  local code=0
  ssh "$HOST" "cd $RDIR && flock -E 75 -w $WAIT $LOCK systemd-run --user --scope --quiet -p MemoryMax=6G -p MemorySwapMax=0 -p CPUQuota=500% -p RuntimeMaxSec=$rt nice -n 15 $*" || code=$?
  if [ "$code" = 75 ]; then echo "forge: lock-ul $LOCK e ținut (QA Unity în curs?); am așteptat ${WAIT}s. Reîncearcă mai târziu." >&2; exit 75; fi
  if [ "$code" != 0 ]; then echo "forge: comanda a ieșit cu $code" >&2; exit "$code"; fi
}
sync() {
  ssh "$HOST" "mkdir -p $RDIR/out/assets"
  rsync -a --delete --exclude out --exclude node_modules --exclude '.DS_Store' --exclude '_takes' "$HERE/" "$HOST:$RDIR/"
  rsync -a --delete "$HERE/out/assets/" "$HOST:$RDIR/out/assets/"
  if [ -d "$HERE/out/footage" ]; then rsync -a "$HERE/out/footage/" "$HOST:$RDIR/out/footage/"; fi
  ssh "$HOST" "cd $RDIR && [ -d node_modules/playwright-core ] || npm i --silent --no-audit --no-fund --no-save playwright-core@1.63.0 >/dev/null"
}
CMD="${1:-}"; shift || true
case "$CMD" in
  sync) sync ;;
  snap)
    NAME="$1"; shift; sync
    cap 300 "bash -c 'rm -rf out/snap/$NAME && node tools/snap.mjs out/snap/$NAME $*'"
    mkdir -p "$HERE/out/snap/$NAME" && rsync -a --delete "$HOST:$RDIR/out/snap/$NAME/" "$HERE/out/snap/$NAME/" ;;
  sheet)
    NAME="$1"; K="${2:-2}"; sync
    cap 420 "bash -c 'rm -rf out/snap/$NAME && node tools/snap.mjs out/snap/$NAME scenes=$K'"
    mkdir -p "$HERE/out/snap/$NAME" && rsync -a --delete "$HOST:$RDIR/out/snap/$NAME/" "$HERE/out/snap/$NAME/" ;;
  render)
    sync
    cap 540 node tools/render.mjs seg "$@" --workers 4 ;;
  mix)
    sync
    cap 120 node tools/render.mjs plan
    cap 300 node tools/mix.mjs out/plan.json out/mix.wav ;;
  final)
    NAME="${1:-darkest-basement-explainer-v1.mp4}"
    cap 120 node tools/render.mjs concat out/video.mp4
    cap 300 ffmpeg -y -v error -i out/video.mp4 -i out/mix.wav -map 0:v -map 1:a -c:v copy -c:a aac -b:a 256k -ar 48000 -movflags +faststart -shortest "out/$NAME"
    ssh "$HOST" "cd $RDIR && ffprobe -v error -show_entries format=duration,size -of default=nw=1 out/$NAME"
    rsync -a "$HOST:$RDIR/out/$NAME" "$HERE/out/$NAME"
    rsync -a "$HOST:$RDIR/out/segments.json" "$HOST:$RDIR/out/plan.json" "$HERE/out/" ;;
  finish)  # plan + mix + concat + mux într-o singură luare a lock-ului (QA-ul îl reia repede între pași)
    NAME="${1:-darkest-basement-explainer-v1.mp4}"
    sync
    cap 540 "bash -c 'node tools/render.mjs plan && node tools/mix.mjs out/plan.json out/mix.wav --stems && node tools/render.mjs concat out/video.mp4 && ffmpeg -y -v error -i out/video.mp4 -i out/mix.wav -map 0:v -map 1:a -c:v copy -c:a aac -b:a 256k -ar 48000 -movflags +faststart -shortest out/$NAME && ffprobe -v error -show_entries format=duration,size -of default=nw=1 out/$NAME'"
    rsync -a "$HOST:$RDIR/out/$NAME" "$HERE/out/$NAME"
    rsync -a "$HOST:$RDIR/out/segments.json" "$HOST:$RDIR/out/plan.json" "$HERE/out/"
    # stem-urile mixajului (voce, efecte, muzică), pentru verificarea sunetului: python3 tools/audio_qa.py out/<nume.mp4>
    mkdir -p "$HERE/out/stems" && rsync -a "$HOST:$RDIR/out/vo.wav" "$HOST:$RDIR/out/sfx.wav" "$HOST:$RDIR/out/music.wav" "$HERE/out/stems/" ;;
  *) echo "comandă necunoscută: $CMD (sync|snap|sheet|render|mix|final|finish)"; exit 2 ;;
esac
