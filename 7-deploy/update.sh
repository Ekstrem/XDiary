#!/usr/bin/env bash

# 2026-11-10 (@DO): определение ДО первого использования — иначе `say: command not found`
# на строке 65 (регресс после merge; исправление уже было 07.11).
say() { printf '%s\n' "$*"; }
die() { printf 'update.sh: ОШИБКА: %s\n' "$*" >&2; exit 1; }

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

# DO-113: выкатка по digest. Тег остаётся для читаемости, но тянем ИМЕННО `@sha256:`
# из `images.lock`: тег можно перезаписать, digest — нельзя (ADR-027).
lock_digest() {
  awk -v s="$1" '!/^#/ && NF>=2 && $1==s {for (i=3;i<=NF;i++) if ($i ~ /^digest=/) {sub(/^digest=/,"",$i); print $i; exit}}' "$LOCK_FILE"
}
pull_by_digest() {
  svc="$1"; tag="$(awk -v s="$svc" '!/^#/ && NF>=2 && $1==s {print $2; exit}' "$LOCK_FILE")"
  dig="$(lock_digest "$svc")"; ref="ghcr.io/ekstrem/xdiary-$svc"
  if [ -n "$tag" ] && [ -n "$dig" ] && [ "$dig" != "-" ]; then
    echo "  pull $svc: $ref@$dig (тег $tag — только для чтения)"
    sudo -n podman pull -q "$ref@$dig" >/dev/null 2>&1 || return 1
  elif [ -n "$tag" ]; then
    echo "  pull $svc: $ref:$tag (digest в lock нет — старый формат)"
    sudo -n podman pull -q "$ref:$tag" >/dev/null 2>&1 || return 1
  fi
  return 0
}

TAG="single"
# @AR 2026-11-06 21:17 + @TL: lock посервисный (`<service> <tag> <path> [commit=…]`),
# поэтому тег берём ДЛЯ КАЖДОГО сервиса, а не один на всех: смешанный набор — норма,
# и один общий тег ломал pull (`xdiary-client:single-240bfba` — тег журнала).
tag_of() {
  awk -v s="$1" '!/^#/ && NF>=2 && $1==s {print $2; exit}' "$LOCK_FILE"
}
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
# DO-130 (@DO 2026-11-13 19:2x): на VPS-1 podman — ROOTLESS (18 контейнеров у `deploy`), а root-podman
# не имеет сети `xdiary` (0 контейнеров, только сеть `podman`). `sudo -n bash recreate-service.sh`
# уводил пересоздание в root-пространство, где `podman run --network xdiary` падает → «не поднялся»
# 3 раза, откат падал так же, и прод оставался на старых контейнерах (Up 9 days).
# Выбираем режим по ФАКТУ: видит ли текущий пользователь контейнеры.
PODMAN_SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  if podman ps --format '{{.Names}}' 2>/dev/null | grep -qx 'xdiary-pg'; then
    PODMAN_SUDO=""          # контейнеры наши (rootless) — sudo не нужен
  else
    PODMAN_SUDO="sudo -n"   # rootless пусто — среда root
  fi
  say "podman-режим: ${PODMAN_SUDO:-rootless (без sudo)}"
fi
HASHFILE="$HOME/.xdiary-images.sha256"
IDFILE="$HOME/.xdiary-image-ids"
DEPLOY_HASH_FILE="$HOME/.xdiary-deploy-sh.sha256"

# DO-074: диск проверяем ДО деплоя. 2026-11-15 на dev закончилось место: контейнеры
# упали, а после освобождения 3.7 ГБ не поднимались — `clone: No space left on device`,
# потому что слой хранения остался битым после записи в полный диск. Проверка дешёвая,
# а восстановление — руками и с простоя.
MIN_FREE_MB="${XDIARY_MIN_FREE_MB:-2048}"
avail_mb=$(df -Pm / | awk 'NR==2 {print $4}')
if [ -n "$avail_mb" ] && [ "$avail_mb" -lt "$MIN_FREE_MB" ]; then
  die "мало места на /: ${avail_mb} МБ < ${MIN_FREE_MB} МБ — деплой остановлен (DO-074)"
fi
say "место на /: ${avail_mb} МБ свободно (порог ${MIN_FREE_MB})"

# DO-072: на хосте с 12+ .NET-сервисов дефолт fs.inotify.max_user_instances=128
# исчерпывается, сервис падает на старте (IOException + ExitCode=139), снаружи — 502.
# Поднимаем лимиты ДО пересоздания контейнеров; скрипт идемпотентен.
HOST_INOTIFY="$(dirname "$0")/host-inotify.sh"
if [ -f "$HOST_INOTIFY" ]; then
  bash "$HOST_INOTIFY" || sudo -n bash "$HOST_INOTIFY" || \
    echo "update.sh: ПРЕДУПРЕЖДЕНИЕ: не удалось поднять inotify-лимиты (DO-072)" >&2
fi


# DO-050 (запрос @TL 21:02): «success» не должен означать «не тот код».
# Сравниваем ВРЕМЯ СБОРКИ образа с временем деплоимого коммита (CODE_TS из CI).
verify_image_age() {
  local svc="$1" img="$2" created created_epoch code_epoch
  [ -n "${CODE_TS:-}" ] || return 0
  code_epoch="$(date -d "$CODE_TS" +%s 2>/dev/null || echo 0)"
  [ "$code_epoch" -gt 0 ] || return 0
  created="$(sudo -n podman image inspect "$img" --format '{{.Created}}' 2>/dev/null || true)"
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
  $PODMAN_SUDO bash "$RECREATE" "$svc" --image "$img" || die "откат не удался"
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

# ---------- перетегировать образы по lock (посервисно) ----------
RETAGGED=""
for img in $IMAGES; do
  svc="${img#ghcr.io/ekstrem/xdiary-}"
  svc="${svc%%:*}"
  name="xdiary-$svc"
  t="$(tag_of "$svc" 2>/dev/null || true)"
  # compose зовёт сервис `clients`, а образ — `xdiary-client` (единственное число):
  # без этого маппинга pull падал на несуществующем `xdiary-clients`, и деплой не ехал.
  if [ -z "$t" ]; then
    s2="${svc%s}"
    t="$(tag_of "$s2" 2>/dev/null || true)"
    [ -n "$t" ] && name="xdiary-$s2"
  fi
  if [ -n "$t" ] && [ "$t" != "$img" ]; then
    # `say` определяется ниже по файлу — здесь обычный echo. Иначе в CI:
    # `update.sh: line 65: say: command not found`, и деплой падал до pull.
    echo "  тег из lock: ${name#xdiary-} → ${t}"
    img="ghcr.io/ekstrem/${name}:${t}"
  fi
  RETAGGED="$RETAGGED $img"
done
IMAGES="$RETAGGED"

# ---------- pull (обязателен) ----------
PULL_FAILED=0
if [ "$DRY" = "1" ]; then
  say "DRY-RUN: pull пропущен"
else
  PULL_ATTEMPTS="${PULL_ATTEMPTS:-3}"
  PULL_PAUSE="${PULL_PAUSE:-10}"
  for img in $IMAGES; do
    # 2026-11-07: контейнеры создаются через `sudo -n podman` (rootful storage), а pull шёл
    # БЕЗ sudo — в rootless storage. Из-за этого `pull не удался` падал то на одном сервисе,
    # то на другом (analytics, apple-health-gateway), а вручную `sudo podman pull` работал.
    #
    # 2026-11-10 (deploy #1912, 14:14:32Z): одна попытка pull на один образ — и весь деплой
    # красный из-за транзиентной ошибки реестра (`catalog:single-e195ad9`), при том что
    # `lock-published` тот же тег видел минутами раньше, а предыдущие прогоны (#1908/#1909/
    # #1911) тянули его нормально. Реестр — внешняя система: 1 попытка = гейт, который
    # падает «не по нашей вине» и который поэтому захочется отключить (§5.4).
    # Ретраим ТОЛЬКО pull (не деплой): провал после всех попыток остаётся красным.
    attempt=1
    pulled=0
    while [ "$attempt" -le "$PULL_ATTEMPTS" ]; do
      if sudo -n podman pull "$img" >/dev/null 2>&1; then
        pulled=1
        [ "$attempt" -gt 1 ] && say "pull ок на попытке $attempt: $img"
        break
      fi
      if [ "$attempt" -lt "$PULL_ATTEMPTS" ]; then
        say "pull: попытка $attempt/$PULL_ATTEMPTS не удалась: $img — пауза ${PULL_PAUSE} с"
        sleep "$PULL_PAUSE"
      fi
      attempt=$((attempt + 1))
    done
    if [ "$pulled" != "1" ]; then
      say "pull не удался после $PULL_ATTEMPTS попыток: $img" >&2
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
  id="$(sudo -n podman image inspect "$img" --format '{{.Id}}' 2>/dev/null || true)"
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
  # 2026-11-07: файлы /tmp/recreate-<svc>.* могли остаться от sudo-прогона и быть root-owned —
  # тогда запись тела падает (PermissionError), тело пустое, сервис «не поднимается».
  # Чистим заранее; пишем те же файлы тоже через sudo, чтобы владелец был один.
  sudo -n rm -f "/tmp/recreate-$svc.body" "/tmp/recreate-$svc.sh" 2>/dev/null || true
  if ! $PODMAN_SUDO bash "$RECREATE" "$svc"; then
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
    $PODMAN_SUDO bash "$RECREATE" "$svc" --image "$old_id" || say "  откат $svc не удался (нужно вручную)"
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

# DO-073: схема БД должна совпадать с миграциями в коде. Иначе «код выехал, колонки нет» —
# именно так 2026-11-14 soft-delete партнёра оказался неработающим при зелёной приёмке.
SCHEMA_CHECK="/opt/xdiary/tools/check_schema.py"
if [ -f "$SCHEMA_CHECK" ]; then
  say "--- проверка схемы (DO-073) ---"
  if ! python3 "$SCHEMA_CHECK" --root /opt/xdiary --container xdiary-pg; then
    echo "update.sh: ОШИБКА: схема БД отстала от кода — примени миграции (DO-073)" >&2
    exit 1
  fi
fi
