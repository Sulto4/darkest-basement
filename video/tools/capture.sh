#!/bin/bash
# Clipurile live pentru sloturile de gameplay, capturate de build-ul de QA pe HP Forge (vezi FOOTAGE.md).
# Pașii lungi (build Unity, run-uri de QA cu -capture-fps, care sub llvmpipe merg cu ~3 cadre/s) rulează ca unități systemd
# detașate pe Forge: iau ACELAȘI lock ca QA-ul și randările (/tmp/darkest-basement-unity.lock), cu plafon de memorie/CPU/timp.
# Pe Mac nu rămâne niciun ssh în fundal: așteptarea e un `flock -w` blocant, limitat în timp. Cadrele nu ajung niciodată pe Mac.
#
#   bash tools/capture.sh push                         → sincronizează repo-ul jocului pe Forge (tools/forge.sh push)
#   bash tools/capture.sh job <nume> <sec> <comandă…>  → pornește <comandă> (în repo-ul jocului de pe Forge) ca unitate detașată
#   bash tools/capture.sh wait [sec=560]               → așteaptă eliberarea lock-ului, apoi arată starea joburilor
#   bash tools/capture.sh status <nume>                → starea unui job + finalul jurnalului + cadrele scrise
#   bash tools/capture.sh clips <script> <prefix>      → fiecare fereastră din qa-log → out/footage/raw/<prefix>-NN.mp4 + planșă
#   bash tools/capture.sh clean <script>               → șterge cadrele de pe Forge (după ce clipurile sunt tăiate)
#   bash tools/capture.sh slots [slot …]               → video/footage.json → out/footage/<slot>.mp4 pe Forge (tools/slots.py)
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd -P)"   # video/
GAME="$(dirname "$HERE")"
HOST="${FORGE_HOST:-hp-forge}"
GDIR="Projects/darkest-basement"              # repo-ul jocului pe Forge (al lui tools/forge.sh)
VDIR="${FORGE_VIDEO_DIR:-Projects/darkest-basement-video}"
LOCK="/tmp/darkest-basement-unity.lock"
CMD="${1:-}"; shift || true
case "$CMD" in
  push) bash "$GAME/tools/forge.sh" push ;;
  job)
    NAME="$1"; RT="$2"; shift 2
    ssh "$HOST" "cd $GDIR && mkdir -p Logs && systemd-run --user --unit=dbvid-$NAME --collect --quiet -p MemoryMax=8G -p MemorySwapMax=0 -p CPUQuota=600% \
      -p RuntimeMaxSec=$RT -p WorkingDirectory=/home/radu/$GDIR /usr/bin/bash -c 'flock $LOCK nice -n 10 env UNITY_NO_CONSENT_PROMPT=1 $* > Logs/job-$NAME.log 2>&1'"
    echo "pornit dbvid-$NAME (plafon ${RT}s); jurnal: $GDIR/Logs/job-$NAME.log" ;;
  wait)
    W="${1:-560}"
    ssh "$HOST" "flock -w $W $LOCK true && echo 'lock liber' || echo 'lock încă ținut'; systemctl --user list-units --all --no-legend 'dbvid-*' | cut -c1-110"
    ;;
  status)
    NAME="$1"
    ssh "$HOST" "systemctl --user is-active dbvid-$NAME 2>/dev/null; tail -4 $GDIR/Logs/job-$NAME.log 2>/dev/null | cut -c1-200; \
      for d in $GDIR/Logs/qa/*/; do n=\$(ls \$d/frames 2>/dev/null | wc -l); [ \$n -gt 0 ] && echo \"\$d: \$n cadre, \$(du -sh \$d/frames | cut -f1)\"; tail -2 \$d/qa-log.txt 2>/dev/null | cut -c1-160; done; df -h ~ | tail -1" ;;
  clips)
    SCRIPT="$1"; PREFIX="$2"
    ssh "$HOST" "mkdir -p $VDIR/out/footage/raw $VDIR/out/footage/review && cd $GDIR/Logs/qa/$SCRIPT && \
      flock -E 75 -w 60 $LOCK systemd-run --user --scope --quiet -p MemoryMax=3G -p MemorySwapMax=0 -p CPUQuota=300% -p RuntimeMaxSec=540 nice -n 15 bash -c '
        n=0; grep -o \"fereastră [^:]*: cadrele [0-9]*–[0-9]*\" qa-log.txt | while read -r _ tags _ range; do
          a=\${range%–*}; b=\${range#*–}; n=\$((n+1)); id=\$(printf \"%s-%02d\" $PREFIX \$n); ext=jpg; [ -f frames/frame_\$(printf %05d \$a).png ] && ext=png
          ffmpeg -nostdin -y -v error -framerate 30 -start_number \$a -i frames/frame_%05d.\$ext -frames:v \$((b-a+1)) -vf scale=1920:1080 \
            -c:v libx264 -preset medium -crf 14 -pix_fmt yuv420p ~/$VDIR/out/footage/raw/\$id.mp4
          ffmpeg -nostdin -y -v error -i ~/$VDIR/out/footage/raw/\$id.mp4 -vf \"select=not(mod(n\\,15)),scale=384:216,tile=6x4:padding=4\" -frames:v 1 ~/$VDIR/out/footage/review/\$id.jpg
          echo \"\$id \${tags%:} \$a-\$b \$((b-a+1)) cadre\"
        done'" | tee "$HERE/out/footage-$PREFIX.txt"
    mkdir -p "$HERE/out/footage/review" && rsync -a "$HOST:$VDIR/out/footage/review/" "$HERE/out/footage/review/" ;;
  clean)
    SCRIPT="$1"
    ssh "$HOST" "rm -rf $GDIR/Logs/qa/$SCRIPT/frames && echo 'cadre șterse: $SCRIPT'; df -h ~ | tail -1" ;;
  slots)
    bash "$HERE/tools/forge.sh" sync
    ssh "$HOST" "cd $VDIR && flock -E 75 -w 60 $LOCK systemd-run --user --scope --quiet -p MemoryMax=4G -p MemorySwapMax=0 -p CPUQuota=400% \
      -p RuntimeMaxSec=540 nice -n 15 python3 tools/slots.py $*" ;;
  *) echo "comandă necunoscută: $CMD (push|job|wait|status|clips|clean|slots)"; exit 2 ;;
esac
