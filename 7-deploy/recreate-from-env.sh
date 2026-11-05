#!/usr/bin/env bash
# recreate-from-env.sh — пересоздать сервис с кредами из secrets/.env (DO-076).
#
# Зачем: развёрнутый `/opt/xdiary/7-deploy/deploy.sh` содержит ВПЕЧАТАННЫЕ строки подключения,
# и в них встречается **dev-хост 77.91.73.67** и устаревшие пароли. Пока сервис не пересоздавался,
# это не проявлялось; при первом пересоздании сервис падает `28P01 password authentication failed`
# (так легли journal, partners, portability — 2026-11-04/05).
#
# Здесь строка подключения собирается ЯВНО: Host=10.89.9.10 (прод-БД) + Database/Username/Password
# из `secrets/.env`. Флаги запуска (сеть, IP, лимиты, read-only, tmpfs, restart) — из карты ниже,
# потому что `sed` по строке `deploy.sh` оказался ненадёжным (проверено: строка не заменялась).
#
#   bash 7-deploy/recreate-from-env.sh catalog portability
#   bash 7-deploy/recreate-from-env.sh --check catalog     # только состояние, без пересоздания
#
# Идемпотентно. Образ — `:single` (тот, что публикует CI).
set -euo pipefail

DEPLOY_DIR="${DEPLOY_DIR:-/opt/xdiary/7-deploy}"
ENV_FILE="secrets/.env"
DB_HOST="${DB_HOST:-10.89.9.10}"
DB_PORT="${DB_PORT:-5432}"
NET=xdiary
WAIT="${WAIT:-20}"
# EXTRA_ENV — дополнительные переменные контейнера в формате "K=V K2=V2" (через пробел).
# Нужно для одноразовых операций (например `DataMigration__Enabled=true` для бэкфилла
# read-модели), чтобы они делались штатным скриптом, а не «руками на сервере».
EXTRA_ENV="${EXTRA_ENV:-}"

cd "$DEPLOY_DIR"
[ -f "$ENV_FILE" ] || { echo "нет $DEPLOY_DIR/$ENV_FILE" >&2; exit 3; }

# сервис: ip|ключ .env|database|username
spec() {
  case "$1" in
    journal)              echo "10.89.9.20|JournalDb|journal|journal" ;;
    partners)             echo "10.89.9.21|PartnersDb|partners|partners" ;;
    catalog)              echo "10.89.9.22|CatalogDb|catalog|catalog" ;;
    somatic-series)       echo "10.89.9.23|SomaticSeriesDb|somatic_series|somatic_series" ;;
    daily-metrics)        echo "10.89.9.24|DailyMetricsDb|daily_metrics|daily_metrics" ;;
    analytics)            echo "10.89.9.27|AnalyticsDb|analytics|analytics" ;;
    privacy)              echo "10.89.9.28|PrivacyDb|privacy|privacy" ;;
    portability)          echo "10.89.9.29|PortabilityDb|portability|portability" ;;
    client)               echo "10.89.9.31|ClientsDb|clients|clients" ;;
    garmin-gateway)       echo "10.89.9.25|GarminGatewayDb|garmin_gateway|garmin_gateway" ;;
    apple-health-gateway) echo "10.89.9.26|AppleHealthGatewayDb|apple_health|apple_health" ;;
    *) echo "" ;;
  esac
}

conn_string() {  # $1 ключ, $2 db, $3 user
  local raw pw
  raw=$(grep -m1 "^ConnectionStrings__$1=" "$ENV_FILE" | cut -d= -f2- | tr -d '\r' || true)
  [ -n "$raw" ] || return 1
  pw=$(printf '%s' "$raw" | sed -E 's/.*Password=([^;]*).*/\1/')
  printf 'Host=%s;Port=%s;Database=%s;Username=%s;Password=%s' "$DB_HOST" "$DB_PORT" "$2" "$3" "$pw"
}

status_of() {
  local n="xdiary-$1"
  printf '  %-22s %-9s restarts=%-6s pw-err=%s\n' "$1" \
    "$(podman inspect "$n" --format '{{.State.Status}}' 2>/dev/null || echo none)" \
    "$(podman inspect "$n" --format '{{.RestartCount}}' 2>/dev/null || echo -)" \
    "$(podman logs --tail 80 "$n" 2>&1 | grep -c 'password authentication failed' || echo 0)"
}

MODE=recreate
if [ "${1:-}" = "--check" ]; then MODE=check; shift; fi

[ "$#" -gt 0 ] || { echo "укажи сервисы: $0 [--check] catalog portability"; exit 2; }

for svc in "$@"; do
  s=$(spec "$svc")
  [ -n "$s" ] || { echo "  ! $svc: нет в карте (добавь ip/db/user)"; continue; }
  IFS='|' read -r ip key db user <<<"$s"

  if [ "$MODE" = check ]; then status_of "$svc"; continue; fi

  cs=$(conn_string "$key" "$db" "$user") || { echo "  ! $svc: в .env нет ConnectionStrings__$key"; continue; }
  echo "== $svc (db=$db user=$user host=$DB_HOST) =="
  podman rm -f "xdiary-$svc" >/dev/null 2>&1 || true
  podman run -d --name "xdiary-$svc" --network "$NET" --ip "$ip" --hostname "$svc" \
    --label io.containers.autoupdate=registry --env-file "$ENV_FILE" \
    -e "ConnectionStrings__CommandDb=$cs" -e "ConnectionStrings__ReadDb=$cs" \
    -e ASPNETCORE_ENVIRONMENT=Single $(for kv in $EXTRA_ENV; do printf ' -e %s' "$kv"; done) \
  --memory 256m --pids-limit 512 \
    --read-only --tmpfs /tmp --restart unless-stopped \
    "ghcr.io/ekstrem/xdiary-$svc:single" >/dev/null \
    && echo "  создан" || echo "  ! запуск не удался"
done

if [ "$MODE" = recreate ]; then
  echo ""
  echo "== состояние через ${WAIT}с =="
  sleep "$WAIT"
  for svc in "$@"; do status_of "$svc"; done
fi
