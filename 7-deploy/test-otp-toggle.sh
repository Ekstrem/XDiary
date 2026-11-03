#!/usr/bin/env bash
# test-otp-toggle.sh — ВРЕМЕННЫЙ тестовый код OTP на проде (DO-046, P0).
#
# Зачем: Telegram недоступен по TCP с обеих VPS, поэтому OTP не доставляется и
# владелец не может войти. По явному разрешению владельца (через @TL, чат 04:25)
# включается фиксированный код `000000` на ОГРАНИЧЕННОЕ время, затем снимается.
#
# Почему нужен флаг AllowTestOtpOutsideDev: `ClientHandlers.cs:18` читает
# `Clients:TestOtp` только в Development; на проде ASPNETCORE_ENVIRONMENT=Single,
# поэтому одного `Clients__TestOtp` НЕДОСТАТОЧНО — он будет молча проигнорирован.
#
# Использование:
#   ./test-otp-toggle.sh status
#   ./test-otp-toggle.sh on  --reason "владелец: вход на прод" --confirm
#   ./test-otp-toggle.sh off
#
# Гарантии:
#   * `on` без `--confirm` отказывает;
#   * перед изменением делается бэкап .env (.env.bak-testotp-<ts>);
#   * трогается только `xdiary-client`, остальные контейнеры не перезапускаются;
#   * `off` удаляет ОБА флага и печатает проверку, что `otpCode` больше не приходит.
set -euo pipefail

ENV_FILE="${ENV_FILE:-/opt/xdiary/7-deploy/secrets/.env}"
CONTAINER="${CONTAINER:-xdiary-client}"
API="${API:-http://127.0.0.1:8080}"
FLAG_ALLOW="Clients__AllowTestOtpOutsideDev"
FLAG_CODE="Clients__TestOtp"
CODE="000000"

say() { printf '%s\n' "$*"; }
die() { printf 'ОШИБКА: %s\n' "$*" >&2; exit 1; }

require_env() {
  [ -f "$ENV_FILE" ] || die "нет файла $ENV_FILE"
}

recreate_container() {
  # ИНЦИДЕНТ 03.11: первая версия удаляла контейнер ДО того, как убедилась, что
  # команда запуска корректна; при сбое генерации прод остался без сервиса clients.
  # Теперь порядок обратный: собрать и проверить команду -> только потом удалять.
  local body="/tmp/xdiary-client-run.sh"
  local runner="/tmp/xdiary-client-recreate.sh"

  python3 - <<'PY' > "$body"
import re
src = open('/home/deploy/deploy.sh', encoding='utf-8').read().replace('\\\n', ' ')
line = next((l for l in src.splitlines() if 'podman run -d --name xdiary-client' in l), '')
print(line.strip())
PY

  if [ ! -s "$body" ]; then
    echo "КРИТИЧНО: не удалось извлечь команду запуска clients из deploy.sh — контейнер НЕ трогаю" >&2
    return 1
  fi
  { echo 'set -u'; echo 'NET=xdiary'
    echo 'LBL="--label io.containers.autoupdate=registry"'
    echo 'TAG=single'; cat "$body"; } > "$runner"
  if ! bash -n "$runner"; then
    echo "КРИТИЧНО: собранная команда не проходит bash -n — контейнер НЕ трогаю" >&2
    return 1
  fi

  podman rm -f "$CONTAINER" >/dev/null 2>&1 || true
  local attempt
  for attempt in 1 2 3; do
    if bash "$runner" >/dev/null 2>&1; then
      sleep 3
      if podman ps --format '{{.Names}}' | grep -qx "$CONTAINER"; then
        return 0
      fi
    fi
    echo "  попытка $attempt не удалась, повтор…" >&2
    sleep 5
  done
  echo "КРИТИЧНО: контейнер $CONTAINER не поднят. Восстановить:" >&2
  echo "  bash $runner" >&2
  return 1
}


probe_api() {
  # возвращает строку с результатом: есть ли otpCode в ответе регистрации
  local phone resp attempt
  phone="+7999$(date +%H%M%S)$RANDOM"
  phone="${phone:0:12}"
  for attempt in 1 2 3 4 5; do
    resp="$(curl -s -m 20 -X POST "$API/api/v1/clients" \
      -H 'Content-Type: application/json' \
      -d "{\"phone\":\"$phone\",\"name\":\"testotp-probe\"}" || true)"
    [ -n "$resp" ] && break
    say "  (попытка $attempt: пустой ответ, жду 4 c)"
    sleep 4
  done
  # ВАЖНО: `otpCode` есть в ЛЮБОМ успешном ответе (он часть агрегата).
  # Признак активного тестового кода — именно значение 000000, иначе проверка врёт.
  if printf '%s' "$resp" | grep -q '"otpCode":"000000"'; then
    say "  ответ API: otpCode=000000 (тестовый код АКТИВЕН)"
    return 0
  fi
  say "  ответ API: otpCode != 000000 (обычный режим)"
  say "  фрагмент ответа: $(printf '%s' "$resp" | head -c 160)"
  return 1
}

case "${1:-}" in
  status)
    say "== статус =="
    say "  файл: $ENV_FILE"
    grep -nE "^($FLAG_ALLOW|$FLAG_CODE)=" "$ENV_FILE" | sed -E 's/=.*/=<задано>/' || say "  тестовые флаги не заданы"
    podman exec "$CONTAINER" sh -c 'echo "  ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-<не задано>}"'
    podman exec "$CONTAINER" sh -c "env | grep -E '^($FLAG_ALLOW|$FLAG_CODE)=' | sed -E 's/=.*/=<задано>/' || echo '  в контейнере флагов нет'"
    probe_api
    ;;

  on)
    reason="${3:-}"; confirm="${4:-}"
    [ "${2:-}" = "--reason" ] || die "нужно: on --reason \"...\" --confirm"
    [ -n "$reason" ] || die "пустая причина"
    [ "$confirm" = "--confirm" ] || die "нужен --confirm (осознанное включение ослабления на проде)"
    require_env
    ts="$(date +%Y%m%d-%H%M%S)"
    cp -a "$ENV_FILE" "$ENV_FILE.bak-testotp-$ts"
    say "бэкап: $ENV_FILE.bak-testotp-$ts"
    grep -vE "^($FLAG_ALLOW|$FLAG_CODE)=" "$ENV_FILE" > "$ENV_FILE.tmp"
    {
      printf '%s=true\n' "$FLAG_ALLOW"
      printf '%s=%s\n' "$FLAG_CODE" "$CODE"
    } >> "$ENV_FILE.tmp"
    mv "$ENV_FILE.tmp" "$ENV_FILE"
    say "включено: $FLAG_ALLOW=true, $FLAG_CODE=$CODE"
    say "причина: $reason"
    say "перезапуск $CONTAINER…"
    recreate_container
    sleep 25
    say "== проверка =="
    if ! probe_api; then
      say ""
      say "!! ФЛАГ ПРОИГНОРИРОВАН ОБРАЗОМ: тестовый код не подхватился."
      say "!! Значит в сборке нет поддержки $FLAG_ALLOW (правка @BE не задеплоена)."
      say "!! Откатываю, чтобы не оставлять «меру, которая не работает»."
      grep -vE "^($FLAG_ALLOW|$FLAG_CODE)=" "$ENV_FILE" > "$ENV_FILE.tmp"
      mv "$ENV_FILE.tmp" "$ENV_FILE"
recreate_container
      die "тестовый код не активировался — нужна правка @BE и деплой"
    fi
    say ""
    say "ЗАПИСАТЬ В DO-046:"
    say "  временная мера: $FLAG_CODE включён $(date -u +%H:%M) UTC по явному разрешению владельца (чат @TL 04:25), причина — Telegram недоступен по TCP с VPS"
    say "  снять сразу после входа владельца, максимум 60 минут"
    ;;

  off)
    require_env
    grep -qE "^($FLAG_ALLOW|$FLAG_CODE)=" "$ENV_FILE" || { say "флагов нет — уже снято"; exit 0; }
    grep -vE "^($FLAG_ALLOW|$FLAG_CODE)=" "$ENV_FILE" > "$ENV_FILE.tmp"
    mv "$ENV_FILE.tmp" "$ENV_FILE"
    say "снято: $FLAG_ALLOW, $FLAG_CODE"
    recreate_container
    sleep 25
    say "== проверка =="
    probe_api
    say ""
    say "ЗАПИСАТЬ В DO-046:"
    say "  снят $(date -u +%H:%M) UTC, проверено: POST /api/v1/clients больше не возвращает otpCode"
    ;;

  *)
    die "использование: $0 status | on --reason \"...\" --confirm | off"
    ;;
esac
