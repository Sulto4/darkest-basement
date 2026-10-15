#!/bin/bash
# Planșe de control pentru clipurile brute (rulează pe Forge), cu secunda din clip scrisă pe fiecare cadru.
#   bash tools/review.sh out/footage/raw/combat-06.mp4 [...]        → out/footage/review2/<id>.jpg (un cadru la 1/3 s)
#   FROM=5 TO=13 STEP=6 bash tools/review.sh out/footage/raw/combat-06.mp4 → out/footage/review2/<id>@5.jpg (fin, pe interval)
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p out/footage/review2
FONT=/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf
STEP="${STEP:-10}"; A=$(python3 -c "print(round(${FROM:-0}*30))"); B=$(python3 -c "print(round(${TO:-9999}*30))")
SUF=""; [ -n "${FROM:-}" ] && SUF="@${FROM}"
for f in "$@"; do
  id=$(basename "$f" .mp4)
  ffmpeg -nostdin -y -v error -i "$f" -vf "select='between(n\,$A\,$B)*not(mod(n-$A\,$STEP))',drawtext=fontfile=$FONT:text='%{pts\:flt}':x=20:y=20:fontsize=72:fontcolor=yellow:box=1:boxcolor=black@0.7,scale=480:270,tile=6x7:padding=3" -frames:v 1 "out/footage/review2/$id$SUF.jpg"
done
