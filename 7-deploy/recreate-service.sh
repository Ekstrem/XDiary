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

# ---------- ожидаемый набор add-host ----------
gen_expected() {
  # ожидаемый список хостов для сервиса $1 (без самого себя)
  local svc="${1:-$SVC}" gen="" raw=""
  for cand in /opt/xdiary/tools/gen-hosts.py "$(dirname "$DEPLOY_SH")/gen-hosts.py"; do
    [ -f "$cand" ] && { gen="$cand"; break; }
  done
  [ -n "$gen" ] || return 0
  raw="$(python3 "$gen" --add-host 2>/dev/null || true)"
  printf '%s' "$raw" | tr ' ' '\n' | grep -v "^--add-host$" | grep -v "^${svc}:" | tr '\n' ' '
}

# ---------- пост-проверка «флаги реально попали в podman» ----------
# Именно этого не хватило 03.11: `--add-host` встал ПОСЛЕ образа (хвостовой `>/dev/null`),
# скрипт печатал «добавлен», а ExtraHosts оставался пустым. Теперь проверяем конфиг.
check_hosts() {
  local svc="$1" expected actual
  expected="$(gen_expected "$svc" | wc -w)"
  if [ "$expected" -eq 0 ]; then
    say "  check-hosts $svc: ожиданий нет (генератор недоступен) — пропуск"
    return 0
  fi
  if ! podman ps --format '{{.Names}}' | grep -qx "xdiary-$svc"; then
    say "  check-hosts $svc: контейнера нет" >&2
    return 1
  fi
  actual="$(podman inspect "xdiary-$svc" --format '{{len .HostConfig.ExtraHosts}}' 2>/dev/null || echo 0)"
  if [ "${actual:-0}" -lt "$expected" ]; then
    say "ОШИБКА: $svc ExtraHosts=$actual, ожидалось $expected — флаги попали НЕ в podman" >&2
    return 1
  fi
  if [ "$svc" = "api-gateway" ]; then
    podman exec "xdiary-$svc" grep -q ' clients$' /etc/hosts 2>/dev/null \
      || { say "ОШИБКА: у шлюза нет псевдонима clients в /etc/hosts (вход ляжет в 502)" >&2; return 1; }
  fi
  say "  check-hosts $svc: ExtraHosts=$actual (ожидалось $expected), clients есть — ok"
  return 0
}

# ---------- режимы проверки (до разбора <service>) ----------
if [ "${1:-}" = "--check-hosts-all" ]; then
  rc=0
  for s in $SVCS_ALL; do check_hosts "$s" || rc=1; done
  exit $rc
fi
if [ "${1:-}" = "--check-hosts" ]; then
  [ -n "${2:-}" ] || die "нужно: --check-hosts <service>"
  check_hosts "$2"
  exit $?
fi

SVC="${1:-}"
IMAGE_OVERRIDE=""
if [ "${2:-}" = "--image" ]; then IMAGE_OVERRIDE="${3:-}"; [ -n "$IMAGE_OVERRIDE" ] || die "пустой --image"; fi

CONTAINER="xdiary-$SVC"
cd "$DEPLOY_DIR" || die "нет $DEPLOY_DIR"

BODY="/tmp/recreate-$SVC.body"
RUNNER="/tmp/recreate-$SVC.sh"

python3 - "$DEPLOY_SH" "$SVC" "$BODY" <<'PY'
import re
import sys
dep, svc, out = sys.argv[1:4]
src = open(dep, encoding='utf-8').read().replace('\\\n', ' ')
needle = 'podman run -d --name xdiary-%s ' % svc
line = next((l for l in src.splitlines() if needle in l), '')
line = line.strip()
# Убираем хвостовые редиректы ДО разбора «последний аргумент = образ».
# Без этого `--image` подменял не образ, а `>/dev/null`: подмена уходила в конец,
# а флаги `--add-host` вставали после настоящего образа → попадали в Cmd
# контейнера, и откат «успешно» создавал контейнер на СТАРОМ образе без add-host
# (инцидент 2026-11-09 22:50: prod client остался без link-partner).
line = re.sub(r'(\s*(?:2?>\s*/dev/null|&>\s*/dev/null|2>&1))+\s*$', '', line)
open(out, 'w', encoding='utf-8').write(line + '\n')
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
  # контроль: последний токен — действительно образ (а не флаг/редирект)
  last_tok="$(tail -1 "$BODY" | awk '{print $NF}')"
  [ "$last_tok" = "$IMAGE_OVERRIDE" ] || die "подмена образа не удалась: в конце строки '$last_tok', ожидали '$IMAGE_OVERRIDE' — контейнер не трогаю"
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
ADD_HOSTS="$(gen_expected)"

{ echo 'set -u'; echo 'NET=xdiary'
  echo 'LBL="--label io.containers.autoupdate=registry"'
  echo 'TAG=single'
  cat "$BODY"; } > "$RUNNER"

if [ -n "$ADD_HOSTS" ] && ! grep -q -- '--add-host' "$RUNNER"; then
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
elif grep -q -- '--add-host' "$RUNNER"; then
  say "  add-host уже в команде $SVC — не дублирую"
fi

bash -n "$RUNNER" || die "собранная команда не проходит bash -n — контейнер не трогаю"

podman rm -f "$CONTAINER" >/dev/null 2>&1 || true
attempt=1
while [ "$attempt" -le 3 ]; do
  if bash "$RUNNER" >/dev/null 2>&1; then
    sleep 3
    if podman ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
      say "  OK: $CONTAINER пересоздан"
      check_hosts "$SVC" || exit 1
      # DO-051 п.2 (@TL 11:45): «пересоздал» = «проверил». Только для шлюза:
      # ExtraHosts=12 ещё не значит, что вход отвечает (псевдоним `clients` мог
      # потеряться -> 502 при живом /health). Проверяем функционально.
      if [ "$SVC" = "api-gateway" ]; then
        sleep 3
        g=$(curl -s -o /dev/null -w '%{http_code}' -m 10 http://127.0.0.1:8080/api/v1/clients || echo 000)
        p=$(curl -s -o /dev/null -w '%{http_code}' -m 10 -X POST -H 'Content-Type: application/json' \
              -d '{}' http://127.0.0.1:8080/api/v1/clients || echo 000)
        case "$g" in 200|401) ;; *) say "ОШИБКА: GET /api/v1/clients = $g (ждали 200/401; 502 = нет псевдонима clients)" >&2; exit 1;; esac
        case "$p" in 200|201|400) ;; *) say "ОШИБКА: POST /api/v1/clients = $p (ждали 200/201/400; 502 = нет псевдонима clients)" >&2; exit 1;; esac
        say "  вход отвечает: GET /clients=$g, POST /clients=$p"
      fi
      exit 0
    fi
  fi
  say "  попытка $attempt не удалась, повтор…"
  attempt=$((attempt + 1))
  sleep 5
done
die "$CONTAINER не поднялся. Восстановить: bash $RUNNER"
