#!/usr/bin/env bash
# Activează licența Unity Personal pe HP Forge: pornește logarea și afișează link-ul.
# Link-ul se deschide în orice browser (PC, telefon); expiră în ~10 minute.
set -euo pipefail
HOST="${FORGE_HOST:-hp-forge}"
ssh "$HOST" 'pkill -f "[u]nity auth login" || true
nohup env UNITY_NO_CONSENT_PROMPT=1 timeout 900 ~/.local/bin/unity auth login --no-banner > /tmp/unity-login.out 2>&1 &
for i in $(seq 1 20); do grep -q "https://" /tmp/unity-login.out && break; sleep 1; done
grep -oE "https://services.api.unity.com/[^ ]+" /tmp/unity-login.out | head -1'
echo
echo "Verificare după logare: ssh $HOST '~/.local/bin/unity auth status; ~/.local/bin/unity license'"
