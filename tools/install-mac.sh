#!/bin/bash
# Instalează Darkest Basement pe macOS cu o singură comandă:
#   curl -fsSL https://raw.githubusercontent.com/Sulto4/darkest-basement/main/tools/install-mac.sh | bash
# Descarcă DMG-ul din ultimul release, copiază jocul în Applications și îl pornește.
# Fișierele descărcate cu curl nu primesc marcajul de carantină, deci macOS nu mai afișează avertismentul Gatekeeper.
set -euo pipefail

URL="https://github.com/Sulto4/darkest-basement/releases/latest/download/DarkestBasement-mac.dmg"
APP="DarkestBasement.app"

# Applications pentru tot Mac-ul dacă avem drept de scriere, altfel cel al utilizatorului.
if [ -n "${DEST:-}" ]; then :
elif [ -w /Applications ]; then DEST="/Applications"
else DEST="$HOME/Applications"; fi
mkdir -p "$DEST"

TMP="$(mktemp -d)"
cleanup() { [ -n "${MNT:-}" ] && hdiutil detach "$MNT" -quiet 2>/dev/null || true; rm -rf "$TMP"; }
trap cleanup EXIT

echo "Descarc Darkest Basement (~58 MB)..."
curl -fL --progress-bar -o "$TMP/game.dmg" "$URL"

MNT="$(hdiutil attach -nobrowse -readonly "$TMP/game.dmg" | tail -1 | awk -F'\t' '{print $NF}')"
rm -rf "$DEST/$APP"
ditto "$MNT/$APP" "$DEST/$APP"
xattr -dr com.apple.quarantine "$DEST/$APP" 2>/dev/null || true

echo "Instalat în $DEST/$APP"
if [ -z "${NO_OPEN:-}" ]; then
  echo "Pornesc jocul..."
  open "$DEST/$APP"
fi
