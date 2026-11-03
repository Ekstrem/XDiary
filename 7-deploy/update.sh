#!/usr/bin/env bash
# update.sh — обновление прода: pull образов, пересоздание ТОЛЬКО изменённых (DO-052).
#
# История (почему так, а не иначе):
#   * DO-034: список сервисов брать из compose, а не перечислять руками — иначе снова
#     забудем сервис (как забыли `clients`: образ не тянулся 18 часов, BE-038 не доехал).
#   * P0 03.11: имя образа != `xdiary-<service>` (`clients` -> `xdiary-client`), пулл
#     `xdiary-clients` давал 403, ошибка глоталась `|| true` → прод 33 часа жил на старом
#     образе, а «деплой» был зелёным. Теперь pull обязателен и валит скрипт.
#   * DO-050: после деплоя проверяем, что контейнеры реально на новых образах.
#   * DO-052: агрегатный хеш заменён на по-образный; пересоздаём только изменённые
#     сервисы (front-only коммит не должен перезапускать бэкенд); откат по компоненту.
#
# Использование (на VPS):
#   bash update.sh [tag] [--dry-run]
#   bash update.sh --rollback <service> <image-id>
set -uo pipefail
SELF_TEST_AGE=0
ARGS=()
for a in "$@"; do
  case "$a" in
    --self-test-age) SELF_TEST_AGE=1 ;;
    --dry-run) DRY=1 ;;
    *) ARGS+=("$a") ;;
  esac
done
# DO-066 / ADR-027: тег из аргумента запрещён — читаем 7-deploy/images.lock (он в git).
if [ "${#ARGS[@]}" -gt 0 ] && [ -n "${ARGS[0]:-}" ]; then
  echo "запрещено: тег задаётся только images.lock (DO-066)" >&2
  exit 2
fi
LOCK_FILE="${LOCK_FILE:-$(dirname "$0")/images.lock}"
TAG="single"
if [ -f "$LOCK_FILE" ]; then
  first="$(awk '!/^#/ && NF>=2 {print $2; exit}' "$LOCK_FILE")"
  [ -n "$first" ] && TAG="$first"
else
  echo "update.sh: нет $LOCK_FILE — деплой невоспроизводим" >&2
  exit 2
fi
DRY="${DRY:-0}"

DEPLOY_DIR=/opt/xdiary/7-deploy
DEPLOY_SH=/home/deploy/deploy.sh
RECREATE=/opt/xdiary/7-deploy/recreate-service.sh
HASHFILE="$HOME/.xdiary-images.sha256"
IDFILE="$HOME/.xdiary-image-ids"
DEPLOY_HASH_FILE="$HOME/.xdiary-deploy-sh.sha256"

say() { printf '%s\n' "$*"; }
die() { printf 'update.sh: ОШИБКА: %s\n' "$*" >&2; exit 1; }

# DO-050 (запрос @TL 21:02): «success» не должен означать «не тот код».
# Сравниваем ВРЕМЯ СБОРКИ образа с временем деплоимого коммита (CODE_TS из CI).
verify_image_age() {
  local svc="$1" img="$2" created created_epoch code_epoch
  [ -n "${CODE_TS:-}" ] || return 0
  code_epoch="$(date -d "$CODE_TS" +%s 2>/dev/null || echo 0)"
  [ "$code_epoch" -gt 0 ] || return 0
  created="$(podman image inspect "$img" --format '{{.Created}}' 2>/dev/null || true)"
  [ -n "$created" ] || return 1
  created_epoch="$(date -d "$(printf '%s' "$created" | sed -E 's/\.[0-9]+//; s/ UTC$//')" +%s 2>/dev/null || echo 0)"
  [ "$created_epoch" -gt 0 ] || return 0
  if [ "$created_epoch" -lt "$code_epoch" ]; then
    say "  ВНИМАНИЕ: $svc — образ собран $(date -d "@$created_epoch" '+%Y-%m-%d %H:%M:%S'), а коммит $(date -d "@$code_epoch" '+%Y-%m-%d %H:%M:%S') → образ СТАРШЕ кода" >&2
    return 1
  fi
  return 0
}

if [ "${1:-}" = "--rollback" ]; then
  svc="${2:-}"; img="${3:-}"
  [ -n "$svc" ] && [ -n "$img" ] || die "нужно: --rollback <service> <image-id>"
  [ -x "$RECREATE" ] || die "нет $RECREATE"
  say "ОТКАТ $svc на $img"
  bash "$RECREATE" "$svc" --image "$img" || die "откат не удался"
  exit 0
fi

cd "$DEPLOY_DIR" || die "нет каталога $DEPLOY_DIR"

# ---------- генератор образов ----------
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GEN=""
for cand in "$HERE/gen-hosts.py" "$HERE/../tools/gen-hosts.py" "/opt/xdiary/tools/gen-hosts.py"; do
  [ -f "$cand" ] && { GEN="$cand"; break; }
done
[ -n "$GEN" ] || die "не найден gen-hosts.py"

# ---------- сверка deploy.sh со снимком (DO-051) ----------
if [ -f "$DEPLOY_SH" ]; then
  CUR=$(sed -E 's/Password=[^;"]*/Password=__REDACTED__/g' "$DEPLOY_SH" | sha256sum | cut -d' ' -f1)
  WANT=$(cat "$DEPLOY_HASH_FILE" 2>/dev/null || echo "")
  if [ -z "$WANT" ]; then
    echo "$CUR" > "$DEPLOY_HASH_FILE"
    say "эталонный хеш deploy.sh создан (первый запуск)"
  elif [ "$CUR" != "$WANT" ]; then
    say "/home/deploy/deploy.sh разошёлся со снимком (sha256 $CUR != $WANT)"
    say "если изменение осознанное — обнови эталон: sed -E 's/Password=[^;\"]*/Password=__REDACTED__/g' $DEPLOY_SH | sha256sum | cut -d' ' -f1 > $DEPLOY_HASH_FILE"
    exit 1
  fi
fi

# ---------- образы из compose ----------
IMAGES="$(python3 "$GEN" --images 2>/dev/null || true)"
[ -n "$IMAGES" ] || die "gen-hosts.py не вернул образы"
case " $IMAGES " in
  *"xdiary-client:"*) ;;
  *) IMAGES="$IMAGES ghcr.io/ekstrem/xdiary-client:$TAG"
     say "compose на диске без clients — образ клиента добавлен явно" >&2 ;;
esac

if [ "$SELF_TEST_AGE" = "1" ]; then
  declare -F verify_image_age >/dev/null || die "нет функции verify_image_age — selftest бессмыслен"
  CODE_TS="${SELF_TEST_CODE_TS:-2030-01-01T00:00:00Z}"
  say "self-test: CODE_TS=$CODE_TS (будущее) — проверка обязана покраснеть"
  IMG="$(printf '%s' "$IMAGES" | awk '{print $1}')"
  if verify_image_age "selftest" "$IMG"; then
    say "ПРОВАЛ: проверка возраста НЕ покраснела на будущем коммите"
    exit 1
  fi
  say "OK: проверка возраста покраснела как должна"
  exit 0
fi

# ---------- pull (обязателен) ----------
PULL_FAILED=0
if [ "$DRY" = "1" ]; then
  say "DRY-RUN: pull пропущен"
else
  for img in $IMAGES; do
    if ! podman pull "$img" >/dev/null 2>&1; then
      say "pull не удался: $img" >&2
      PULL_FAILED=$((PULL_FAILED + 1))
    fi
  done
  [ "$PULL_FAILED" -eq 0 ] || die "не удалось подтянуть $PULL_FAILED образ(ов) — не деплою частичный набор"
fi

# ---------- по-образные ID и сравнение с прошлым состоянием ----------
declare -A CUR_IDS=()
NEW_STATE=""
MISSING=0
for img in $IMAGES; do
  id="$(podman image inspect "$img" --format '{{.Id}}' 2>/dev/null || true)"
  if [ -z "$id" ]; then
    say "образ $img отсутствует локально" >&2
    MISSING=$((MISSING + 1))
    continue
  fi
  svc="$(printf '%s' "$img" | sed -E 's#.*/xdiary-([a-z-]+):.*#\1#')"
  verify_image_age "$svc" "$img" || STALE_IMAGES="${STALE_IMAGES:-} $svc"
  CUR_IDS["$svc"]="$id"
  NEW_STATE="${NEW_STATE}${svc} ${id}"$'\n'
done
[ "$MISSING" -eq 0 ] || die "$MISSING образ(ов) не найдено — не деплою"

if [ -n "${STALE_IMAGES:-}" ] && [ "$DRY" != "1" ]; then
  die "образ(ы) старше кода:$STALE_IMAGES — деплой бессмысленен (это «success» на старом коде)"
fi

OLD_STATE="$(cat "$IDFILE" 2>/dev/null || true)"

# ---------- какие сервисы изменились ----------
CHANGED=""
for svc in "${!CUR_IDS[@]}"; do
  old_id="$(printf '%s' "$OLD_STATE" | awk -v s="$svc" '$1==s {print $2}')"
  if [ "$old_id" != "${CUR_IDS[$svc]}" ]; then
    CHANGED="$CHANGED $svc"
  fi
done
CHANGED="$(echo "$CHANGED" | tr ' ' '\n' | sed '/^$/d' | sort | tr '\n' ' ')"

if [ -z "$CHANGED" ]; then
  say "изменений нет (по-образное сравнение): деплой не требуется"
  [ "$DRY" = "1" ] || printf '%s' "$NEW_STATE" > "$IDFILE"
  exit 0
fi
say "изменились: $CHANGED"
if [ "$DRY" = "1" ]; then
  say "DRY-RUN: пересоздание не выполняется"
  exit 0
fi

# ---------- пересоздание только изменённых, в порядке: сервисы → шлюз ----------
ORDER="journal partners catalog somatic-series daily-metrics garmin-gateway apple-health-gateway analytics privacy portability client api-gateway"
FAILED=""
for svc in $ORDER; do
  case " $CHANGED " in *" $svc "*) ;; *) continue ;; esac
  say "  пересоздаю $svc"
  if ! bash "$RECREATE" "$svc"; then
    say "  $svc не поднялся — прекращаю и откатываю остальные"
    FAILED="$svc"
    break
  fi
done

# ---------- пост-проверка ----------
if [ -n "$FAILED" ]; then
  say "провал на $FAILED: откат на предыдущие образы"
  for svc in $ORDER; do
    case " $CHANGED " in *" $svc "*) ;; *) continue ;; esac
    old_id="$(printf '%s' "$OLD_STATE" | awk -v s="$svc" '$1==s {print $2}')"
    [ -n "$old_id" ] || continue
    say "  откат $svc → $old_id"
    bash "$RECREATE" "$svc" --image "$old_id" || say "  откат $svc не удался (нужно вручную)"
  done
  exit 1
fi

if ! curl -fsS -m 8 http://127.0.0.1:8080/health >/dev/null 2>&1; then
  say "после деплоя /health не 200 — это дефект доставки, разбираемся (не «докручиваем на живом»)"
  exit 1
fi

printf '%s' "$NEW_STATE" > "$IDFILE"
printf '%s' "$(echo "$NEW_STATE" | sha256sum | cut -d' ' -f1)" > "$HASHFILE"
say "OK: обновлены $CHANGED; /health 200"
