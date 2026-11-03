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

# генератор может лежать в трёх местах: рядом со скриптом, в tools/ репозитория
# или в развёрнутом /opt/xdiary/tools. Ищем первый существующий, чтобы скрипт
# не зависел от того, как именно разложен деплой.
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GEN=""
for cand in "$HERE/gen-hosts.py" "$HERE/../tools/gen-hosts.py" "/opt/xdiary/tools/gen-hosts.py"; do
  if [ -f "$cand" ]; then GEN="$cand"; break; fi
done
SVCS="$(python3 "$GEN" --svcs 2>/dev/null || true)"
if [ -z "$SVCS" ]; then
  echo "update.sh: не смог получить список сервисов из $GEN — выхожу, чтобы не обновить неполный набор" >&2
  exit 1
fi
echo "update.sh: сервисов $(echo "$SVCS" | wc -w) (из gen-hosts.py): $SVCS"

# P0 03.11: имя образа != `xdiary-<service>` (сервис `clients` -> образ `xdiary-client`).
# Раньше здесь собиралось имя из сервиса, пулл `xdiary-clients` давал 403, ошибка
# глоталась `|| true`, и контейнер клиента 33 часа жил на старом образе.
IMAGES="$(python3 "$GEN" --images 2>/dev/null || true)"
if [ -z "$IMAGES" ]; then
  echo "update.sh: не смог получить образы из $GEN — выхожу" >&2
  exit 1
fi
# Страховка от дрейфа compose на сервере: если развёрнутый podman-compose.yml
# старее репозитория и в нём нет сервиса `clients`, генератор не вернёт образ
# клиента. Именно так прод 33 часа жил на старом образе. Добавляем явно.
case " $IMAGES " in
  *"xdiary-client:"*) ;;
  *) IMAGES="$IMAGES ghcr.io/ekstrem/xdiary-client:$TAG"
     echo "update.sh: compose на диске без clients — образ клиента добавлен явно" >&2 ;;
esac

for img in $IMAGES; do
  podman pull "$img" >/dev/null 2>&1 || echo "update.sh: pull не удался: $img" >&2
done

NEWHASH=$(for img in $IMAGES; do podman image inspect "$img" --format '{{.Id}}' 2>/dev/null; done | sha256sum | cut -d' ' -f1)
OLDHASH=$(cat "$HASHFILE" 2>/dev/null || echo "")
if [ "$NEWHASH" != "$OLDHASH" ]; then
  echo "$(date +%F_%T) image change detected, redeploying" >> "$HOME/update.log"
  bash /opt/xdiary/7-deploy/deploy.sh "$TAG" >> "$HOME/update.log" 2>&1
  echo "$NEWHASH" > "$HASHFILE"
else
  echo "$(date +%F_%T) no changes" >> "$HOME/update.log"
fi
