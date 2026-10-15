#!/bin/bash
# Taie un clip live pentru un slot de gameplay din cadrele PNG ale build-ului de QA (-capture-fps 30) și îl pune în
# out/footage/<slot>.mp4. Rulează pe Forge, unde sunt cadrele, sub același lock și plafon ca randările.
#
#   bash tools/footage.sh <slot> <dir-cadre-pe-Forge> <primul-cadru> [cadre=durata slotului]
#   ex.: bash tools/footage.sh s10-arc /home/radu/Projects/darkest-basement/Logs/qa/tour/frames 2412
#
# Numărul de cadre implicit e lungimea slotului din out/segments.json (scrisă de render.mjs plan / forge.sh mix).
# Dacă clipul e mai scurt, ultimul cadru se ține; dacă e mai lung, se taie. Apoi: bash tools/forge.sh render <slot>.
set -euo pipefail
HERE="$(cd "$(dirname "$0")/.." && pwd -P)"
SLOT="$1"; DIR="$2"; START="$3"; N="${4:-}"
if [ -z "$N" ]; then
  N=$(python3 -c "import json,sys; s=[x for x in json.load(open('$HERE/out/segments.json'))['segments'] if x['id']=='$SLOT']; print(s[0]['f1']-s[0]['f0'] if s else '')")
  [ -n "$N" ] || { echo "slotul $SLOT nu e în out/segments.json"; exit 2; }
fi
RDIR="${FORGE_VIDEO_DIR:-Projects/darkest-basement-video}"
ssh hp-forge "mkdir -p $RDIR/out/footage && flock -E 75 -w 60 /tmp/darkest-basement-unity.lock systemd-run --user --scope --quiet -p MemoryMax=3G -p MemorySwapMax=0 -p CPUQuota=300% -p RuntimeMaxSec=300 nice -n 15 \
  ffmpeg -y -v error -framerate 30 -start_number $START -i '$DIR/frame_%05d.png' -frames:v $N -vf 'scale=1920:1080:flags=neighbor' \
  -c:v libx264 -preset medium -crf 16 -pix_fmt yuv420p $RDIR/out/footage/$SLOT.mp4"
mkdir -p "$HERE/out/footage" && rsync -a "hp-forge:$RDIR/out/footage/$SLOT.mp4" "$HERE/out/footage/"
echo "gata: out/footage/$SLOT.mp4 ($N cadre). Urmează: bash tools/forge.sh render $SLOT && bash tools/forge.sh final"
