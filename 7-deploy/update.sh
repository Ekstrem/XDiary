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

PULL_FAILED=0
for img in $IMAGES; do
  if ! podman pull "$img" >/dev/null 2>&1; then
    echo "update.sh: ОШИБКА pull: $img" >&2
    PULL_FAILED=$((PULL_FAILED + 1))
  fi
done
if [ "$PULL_FAILED" -gt 0 ]; then
  # Раньше здесь стояло `|| echo`: 33 часа прод жил на старом образе, а «деплой» был зелёным.
  echo "update.sh: не удалось подтянуть $PULL_FAILED образ(ов) — выхожу, чтобы не деплоить частичный набор" >&2
  exit 1
fi

# DO-050: пустой ID раньше «растворялся» в хеше и был неотличим от «образа нет».
HASH_PARTS=""
MISSING=0
for img in $IMAGES; do
  id="$(podman image inspect "$img" --format '{{.Id}}' 2>/dev/null || true)"
  if [ -z "$id" ]; then
    echo "update.sh: ОШИБКА — образ $img отсутствует локально после pull" >&2
    MISSING=$((MISSING + 1))
    continue
  fi
  HASH_PARTS="$HASH_PARTS$id"
done
if [ "$MISSING" -gt 0 ]; then
  echo "update.sh: $MISSING образ(ов) не найдено — не деплою" >&2
  exit 1
fi
NEWHASH=$(printf '%s' "$HASH_PARTS" | sha256sum | cut -d' ' -f1)
OLDHASH=$(cat "$HASHFILE" 2>/dev/null || echo "")
# DO-050: «deploy прошёл» != «на сервере новый образ». Проверяем фактически:
# образ каждого контейнера должен совпадать с локальным тегом, который мы только что
# подтянули. Именно этого не хватило 03.11: update.sh «успешно» отработал, а
# xdiary-client 33 часа жил на образе от 01.11, потому что pull тихо падал.
verify_running_images() {
  # Смысл: сравнить ID образа, из которого РЕАЛЬНО запущен контейнер, с ID,
  # на который сейчас указывает его тег (его мы только что подтянули).
  # Совпало — новый образ доехал; не совпало — контейнер остался старым.
  local fail=0 cname tag actual_id tag_id
  for cname in $(podman ps --format '{{.Names}}' 2>/dev/null); do
    case "$cname" in xdiary-*) ;; *) continue ;; esac
    tag="$(podman inspect "$cname" --format '{{.ImageName}}' 2>/dev/null || true)"
    actual_id="$(podman inspect "$cname" --format '{{.Image}}' 2>/dev/null || true)"
    if [ -z "$tag" ] || [ -z "$actual_id" ]; then
      echo "verify: $cname — не смог прочитать образ/ID" >&2
      fail=1
      continue
    fi
    tag_id="$(podman image inspect "$tag" --format '{{.Id}}' 2>/dev/null || true)"
    if [ -z "$tag_id" ]; then
      echo "verify: $cname — тег $tag отсутствует локально" >&2
      fail=1
      continue
    fi
    if [ "$actual_id" != "$tag_id" ]; then
      echo "verify: $cname работает на СТАРОМ образе (${actual_id:0:12} != ${tag_id:0:12}, тег $tag)" >&2
      fail=1
    fi
  done
  return $fail
}


if [ "$NEWHASH" != "$OLDHASH" ]; then
  echo "$(date +%F_%T) image change detected, redeploying" >> "$HOME/update.log"
  bash /opt/xdiary/7-deploy/deploy.sh "$TAG" >> "$HOME/update.log" 2>&1
  if ! verify_running_images; then
    echo "$(date +%F_%T) ОШИБКА: после деплоя образы контейнеров не совпали с локальными" >> "$HOME/update.log"
    echo "update.sh: ОШИБКА — контейнеры работают не на тех образах, что подтянуты (см. выше)" >&2
    exit 1
  fi
  echo "$NEWHASH" > "$HASHFILE"
else
  echo "$(date +%F_%T) no changes" >> "$HOME/update.log"
fi
