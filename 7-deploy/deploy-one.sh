#!/usr/bin/env bash
# deploy-one.sh — ТОЧЕЧНАЯ выкатка одного сервиса (DO-110).
#
# Зачем: инцидент 2026-11-06. Ручная реконструкция `podman run` из `podman inspect`
# теряла `--network/--ip/--read-only/--env-file` и строки подключения, `podman pull`
# падал (125), контейнер уже был удалён → `POST /api/v1/clients` отдавал 500 ~20 минут,
# при этом `/health` оставался 200 (его отдаёт Caddy). Здесь ручная сборка запрещена:
# все флаги берутся из `recreate-service.sh` (он читает строку запуска из `deploy.sh`
# и пароль из `secrets/.env`), а тег — из `images.lock`.
#
# Использование:
#   ./deploy-one.sh <service>                 # тег из images.lock
#   ./deploy-one.sh <service> --tag <tag>     # явный тег (например из lock другого коммита)
#   ./deploy-one.sh <service> --dry-run       # только показать план, ничего не менять
#   ./deploy-one.sh <service> --rollback      # вернуть предыдущий образ (по сохранённому id)
#   ./deploy-one.sh --list                    # сервисы, которые знает deploy.sh
#
# Гарантии (проверяются в прогоне, а не «на словах»):
#   1) `podman pull` выполняется ДО любого `rm` — если образ не скачался, работающий
#      контейнер остаётся на месте, скрипт выходит с rc=1 (дефект-плечо в приёмке);
#   2) после пересоздания проверяется `State.Status == running`, `RestartCount == 0`
#      и `/health` — иначе rc=1 и печатается команда откатa;
#   3) `--rollback` не «собирает» команду руками: берёт id предыдущего образа из
#      `deploy-one.state` и передаёт его в `recreate-service.sh --image`.
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
RECREATE="${RECREATE:-$HERE/recreate-service.sh}"
LOCK_FILE="${LOCK_FILE:-$HERE/images.lock}"
STATE_FILE="${STATE_FILE:-$HERE/deploy-one.state}"
CONTAINER_PREFIX="${CONTAINER_PREFIX:-xdiary-}"
HEALTH_URL="${HEALTH_URL:-http://127.0.0.1:8080/health}"
PULL_ATTEMPTS="${PULL_ATTEMPTS:-3}"
PULL_PAUSE="${PULL_PAUSE:-5}"

say() { echo "[deploy-one] $*"; }
die() { echo "[deploy-one] ОШИБКА: $*" >&2; exit 1; }

[ -x "$RECREATE" ] || [ -f "$RECREATE" ] || die "нет $RECREATE (recreate-service.sh) — точечная выкатка невозможна"
[ -f "$LOCK_FILE" ] || die "нет $LOCK_FILE (images.lock) — тег неизвестен, выкатывать нечего"

tag_of() { awk -v s="$1" '!/^#/ && NF>=2 && $1==s {print $2; exit}' "$LOCK_FILE"; }

case "${1:-}" in
  ""|--list)
    if [ "${1:-}" = "--list" ]; then
      exec bash "$RECREATE" --list
    fi
    die "нужно: ./deploy-one.sh <service> [--tag <tag>|--rollback|--dry-run]"
    ;;
esac

SVC="$1"; shift
TAG=""; MODE="deploy"
while [ "$#" -gt 0 ]; do
  case "$1" in
    --tag) TAG="${2:-}"; shift 2 ;;
    --rollback) MODE="rollback"; shift ;;
    --dry-run) MODE="dry"; shift ;;
    *) die "неизвестный аргумент: $1" ;;
  esac
done

CONTAINER="$CONTAINER_PREFIX$SVC"

if [ "$MODE" = "rollback" ]; then
  [ -f "$STATE_FILE" ] || die "нет $STATE_FILE — предыдущий образ не зафиксирован (откат невозможен)"
  PREV="$(awk -v s="$SVC" '$1==s {print $2; exit}' "$STATE_FILE")"
  [ -n "$PREV" ] || die "в $STATE_FILE нет прошлого образа для $SVC"
  say "откат $SVC на предыдущий образ $PREV"
  if [ "$MODE" = "dry" ]; then exit 0; fi
  bash "$RECREATE" "$SVC" --image "$PREV" || die "откат не удался (rc=$?)"
  exit 0
fi

TAG="${TAG:-$(tag_of "$SVC")}"
[ -n "$TAG" ] || die "сервиса $SVC нет в $LOCK_FILE — тег неизвестен"
IMG="ghcr.io/ekstrem/xdiary-$SVC:$TAG"

CUR_IMG="$(sudo -n podman inspect "$CONTAINER" --format '{{.ImageName}}' 2>/dev/null || true)"
say "сервис: $SVC | тег: $TAG | образ: $IMG"
say "сейчас: ${CUR_IMG:-<контейнера нет>}"

if [ "$MODE" = "dry" ]; then
  say "DRY-RUN: pull + recreate-service.sh $SVC (контейнер не тронут) — образ $IMG"
  exit 0
fi

# 1) PULL ДО rm: если образ не скачался — работающий контейнер не трогаем.
attempt=1; pulled=0
while [ "$attempt" -le "$PULL_ATTEMPTS" ]; do
  if sudo -n podman pull -q "$IMG" >/dev/null 2>&1; then pulled=1; break; fi
  say "pull: попытка $attempt/$PULL_ATTEMPTS не удалась — пауза ${PULL_PAUSE} с"
  attempt=$((attempt + 1)); sleep "$PULL_PAUSE"
done
if [ "$pulled" != "1" ]; then
  say "pull не удался после $PULL_ATTEMPTS попыток: $IMG"
  say "контейнер $CONTAINER НЕ тронут (так и задумано: pull до rm)"
  exit 1
fi

# 2) фиксируем текущий образ для откатa (до пересоздания)
if [ -n "$CUR_IMG" ]; then
  CUR_ID="$(sudo -n podman image inspect "$CUR_IMG" --format '{{.Id}}' 2>/dev/null || true)"
  if [ -n "$CUR_ID" ]; then
    { echo "$SVC $CUR_ID"; } >> "$STATE_FILE"
    say "для откатa записан id: $CUR_ID (файл $STATE_FILE)"
  fi
fi

# 3) пересоздание штатным путём (флаги/сеть/env — из recreate-service.sh, не из inspect)
bash "$RECREATE" "$SVC" || die "recreate-service.sh $SVC вернул rc=$?"

# 4) пост-проверка: running, restarts=0, /health
STATE="$(sudo -n podman inspect "$CONTAINER" --format '{{.State.Status}}' 2>/dev/null || true)"
RESTARTS="$(sudo -n podman inspect "$CONTAINER" --format '{{.RestartCount}}' 2>/dev/null || true)"
HEALTH="$(curl -s -o /dev/null -m 10 -w '%{http_code}' "$HEALTH_URL" 2>/dev/null || echo 000)"
say "проверка: status=${STATE:-?} restarts=${RESTARTS:-?} health=$HEALTH"
if [ "$STATE" != "running" ] || [ "${RESTARTS:-x}" != "0" ] || [ "$HEALTH" != "200" ]; then
  say "сервис не поднялся штатно — откат: ./deploy-one.sh $SVC --rollback"
  exit 1
fi
say "готово: $SVC на $IMG (running, restarts=0, health 200)"
