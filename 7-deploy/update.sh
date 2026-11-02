#!/usr/bin/env bash
# DO-034: список сервисов берётся из tools/gen-hosts.py (он читает podman-compose.yml),
# а НЕ перечисляется руками. Иначе снова забудем сервис — как забыли `clients`:
# образ клиента не тянулся 18 часов, BE-038 не доехал, front-e2e падал с 401.
#
# Использование (на VPS): bash update.sh [tag]
set -uo pipefail
TAG="${1:-single}"
cd /opt/xdiary/7-deploy || exit 1
HASHFILE="$HOME/.xdiary-images.sha256"

GEN="$HOME/XDiary/tools/gen-hosts.py"
SVCS="$(python3 "$GEN" --svcs 2>/dev/null || true)"
if [ -z "$SVCS" ]; then
  echo "update.sh: не смог получить список сервисов из $GEN — выхожу, чтобы не обновить неполный набор" >&2
  exit 1
fi
echo "update.sh: сервисов $(echo "$SVCS" | wc -w) (из gen-hosts.py): $SVCS"

for s in $SVCS; do
  sudo podman pull "ghcr.io/ekstrem/xdiary-$s:$TAG" >/dev/null 2>&1 || true
done

NEWHASH=$(for s in $SVCS; do sudo podman image inspect "ghcr.io/ekstrem/xdiary-$s:$TAG" --format '{{.Id}}' 2>/dev/null; done | sha256sum | cut -d' ' -f1)
OLDHASH=$(cat "$HASHFILE" 2>/dev/null || echo "")
if [ "$NEWHASH" != "$OLDHASH" ]; then
  echo "$(date +%F_%T) image change detected, redeploying" >> "$HOME/update.log"
  bash /opt/xdiary/7-deploy/deploy.sh "$TAG" >> "$HOME/update.log" 2>&1
  echo "$NEWHASH" > "$HASHFILE"
else
  echo "$(date +%F_%T) no changes" >> "$HOME/update.log"
fi
