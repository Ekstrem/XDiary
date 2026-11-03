#!/usr/bin/env bash
# recreate-service.sh — пересоздать ОДИН сервис из строки запуска в deploy.sh (DO-052).
#
# Зачем: `deploy.sh` пересоздаёт ВСЕ контейнеры. Для частичного деплоя нужно уметь
# пересоздавать только изменённые — иначе front-only коммит перезапускает бэкенд.
#
# Использование:
#   ./recreate-service.sh <service>              # как в deploy.sh (тег single)
#   ./recreate-service.sh <service> --image <id> # откат на конкретный digest/ID
#   ./recreate-service.sh --list                 # какие сервисы знает deploy.sh
#
# Гарантии:
#   * сначала собираем и валидируем команду (bash -n), только потом удаляем контейнер;
#   * 3 попытки; при провале — печатаем готовую команду восстановления и выходим с 1.
set -uo pipefail

DEPLOY_SH="${DEPLOY_SH:-/home/deploy/deploy.sh}"
DEPLOY_DIR="${DEPLOY_DIR:-/opt/xdiary/7-deploy}"
SVCS_ALL="journal partners catalog somatic-series daily-metrics garmin-gateway apple-health-gateway analytics privacy portability client api-gateway"

say() { printf '%s\n' "$*"; }
die() { printf 'recreate-service: ОШИБКА: %s\n' "$*" >&2; exit 1; }

[ -f "$DEPLOY_SH" ] || die "нет $DEPLOY_SH"

if [ "${1:-}" = "--list" ]; then
  for s in $SVCS_ALL; do
    grep -q "podman run -d --name xdiary-$s " "$DEPLOY_SH" && echo "$s"
  done
  exit 0
fi

SVC="${1:-}"
[ -n "$SVC" ] || die "нужно: <service> [--image <id>]"
IMAGE_OVERRIDE=""
if [ "${2:-}" = "--image" ]; then IMAGE_OVERRIDE="${3:-}"; [ -n "$IMAGE_OVERRIDE" ] || die "пустой --image"; fi

CONTAINER="xdiary-$SVC"
cd "$DEPLOY_DIR" || die "нет $DEPLOY_DIR"

BODY="/tmp/recreate-$SVC.body"
RUNNER="/tmp/recreate-$SVC.sh"

python3 - "$DEPLOY_SH" "$SVC" "$BODY" <<'PY'
import sys
dep, svc, out = sys.argv[1:4]
src = open(dep, encoding='utf-8').read().replace('\\\n', ' ')
needle = 'podman run -d --name xdiary-%s ' % svc
line = next((l for l in src.splitlines() if needle in l), '')
open(out, 'w', encoding='utf-8').write(line.strip() + '\n')
PY

[ -s "$BODY" ] || die "в $DEPLOY_SH нет команды для $SVC — контейнер не трогаю"

# подмена образа для отката: последний аргумент строки — ghcr.io/...:<tag>
if [ -n "$IMAGE_OVERRIDE" ]; then
  python3 - "$BODY" "$IMAGE_OVERRIDE" <<'PY'
import sys
path, img = sys.argv[1:3]
line = open(path, encoding='utf-8').read().strip()
parts = line.rsplit(' ', 1)
assert len(parts) == 2, 'нет образа в конце строки'
open(path, 'w', encoding='utf-8').write(parts[0] + ' ' + img + '\n')
PY
  say "  образ переопределён на $IMAGE_OVERRIDE (откат)"
fi

# DO-048: сервисы обращаются к соседям по КОРОТКОМУ имени (`journal:8080`), а CNI-DNS
# отдаёт только `xdiary-journal.dns.podman`. В `deploy.sh` `--add-host` есть ТОЛЬКО у
# api-gateway, поэтому у analytics падал precompute:
# `SocketException: Name or service not known (journal:8080)`.
# Добавляем add-host автоматически из gen-hosts.py --add-host — для любого сервиса.
GEN=""
for cand in /opt/xdiary/tools/gen-hosts.py "$(dirname "$DEPLOY_SH")/gen-hosts.py"; do
  [ -f "$cand" ] && { GEN="$cand"; break; }
done
ADD_HOSTS=""
if [ -n "$GEN" ]; then
  ADD_HOSTS="$(python3 "$GEN" --add-host 2>/dev/null || true)"
  # сам себя в hosts не нужен
  ADD_HOSTS="$(printf '%s' "$ADD_HOSTS" | tr ' ' '\n' | grep -v "^--add-host$" | grep -v "^${SVC}:" | tr '\n' ' ')"
fi

{ echo 'set -u'; echo 'NET=xdiary'
  echo 'LBL="--label io.containers.autoupdate=registry"'
  echo 'TAG=single'
  cat "$BODY"; } > "$RUNNER"

if [ -n "$ADD_HOSTS" ]; then
  # вставляем --add-host ... перед образом (последний аргумент строки)
  python3 - "$RUNNER" "$ADD_HOSTS" <<'PY'
import sys
runner, hosts = sys.argv[1:3]
lines = open(runner, encoding='utf-8').read().splitlines()
out = []
for l in lines:
    if 'podman run -d --name' in l:
        parts = l.rsplit(' ', 1)
        if len(parts) == 2:
            add = ' '.join('--add-host ' + h for h in hosts.split() if ':' in h)
            l = parts[0] + ' ' + add + ' ' + parts[1]
    out.append(l)
open(runner, 'w', encoding='utf-8').write('\n'.join(out) + '\n')
PY
  say "  add-host добавлен для $SVC ($(printf '%s' "$ADD_HOSTS" | wc -w) хостов)"
fi

bash -n "$RUNNER" || die "собранная команда не проходит bash -n — контейнер не трогаю"

podman rm -f "$CONTAINER" >/dev/null 2>&1 || true
attempt=1
while [ "$attempt" -le 3 ]; do
  if bash "$RUNNER" >/dev/null 2>&1; then
    sleep 3
    if podman ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
      say "  OK: $CONTAINER пересоздан"
      exit 0
    fi
  fi
  say "  попытка $attempt не удалась, повтор…"
  attempt=$((attempt + 1))
  sleep 5
done
die "$CONTAINER не поднялся. Восстановить: bash $RUNNER"
