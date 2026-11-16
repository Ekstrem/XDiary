#!/usr/bin/env bash
# fix-migrations-history.sh — снять рестарт-петлю journal/partners (42P07), DO-135.
#
# Проблема: в __EFMigrationsHistory нет строк для уже применённых миграций AddReadStores,
# поэтому EF на старте делает CREATE TABLE, получает 42P07 (duplicate_table) и сервис падает.
# Решение: отметить эти миграции как применённые в БД journal и partners (DDL не меняется),
# затем перезапустить сервисы. Обратимо: DELETE этих строк возвращает прежнее поведение.
#
# Запуск на VPS-1: bash /opt/xdiary/7-deploy/fix-migrations-history.sh [--dry-run]
set -uo pipefail

PG="${XDIARY_PG:-xdiary-pg}"
DRY=0
[ "${1:-}" = "--dry-run" ] && DRY=1

MIGS="20261105170525_AddReadStores 20261105170447_AddReadStores"

conn_for() { # $1 = имя контейнера сервиса → печатает строку подключения
  # ВАЖНО (прогон 43839429106): `podman exec printenv` вернул пусто — контейнер может быть
  # в рестарт-петле или в образе нет printenv. Читаем конфиг контейнера: `podman inspect`
  # работает и для остановленного контейнера и отдаёт фактические env-переменные.
  podman inspect "$1" --format '{{range .Config.Env}}{{println .}}{{end}}' 2>/dev/null \
    | sed -n 's/^ConnectionStrings__CommandDb=//p' | head -1
}

echo "fix-migrations-history: сервисы journal partners${DRY:+ (dry-run=$DRY)}"
for svc in journal partners; do
  ctr="xdiary-$svc"
  cs=$(conn_for "$ctr")
  if [ -z "${cs:-}" ]; then echo "  $svc: не смог прочитать ConnectionStrings__CommandDb из $ctr" >&2; continue; fi
  db=$(printf '%s' "$cs" | tr ';' '\n' | sed -n 's/^Database=//p')
  usr=$(printf '%s' "$cs" | tr ';' '\n' | sed -n 's/^Username=//p')
  pwd_=$(printf '%s' "$cs" | tr ';' '\n' | sed -n 's/^Password=//p')
  host=$(printf '%s' "$cs" | tr ';' '\n' | sed -n 's/^Host=//p')
  port=$(printf '%s' "$cs" | tr ';' '\n' | sed -n 's/^Port=//p')
  # Фолбэк: deploy.sh создаёт БД и роль по имени сервиса (Database=journal;Username=journal)
  [ -n "${db:-}" ] || db="$svc"
  [ -n "${usr:-}" ] || usr="$svc"
  if [ -z "${cs:-}" ]; then
    echo "  $svc: строка подключения не прочитана — беру фолбэк db=$db usr=$usr (deploy.sh: Database=Username=<svc>)" >&2
  fi
  if [ -z "${pwd_:-}" ]; then
    echo "  $svc: пароль не найден в конфиге контейнера — прочитаю из secrets/.env" >&2
    for f in /opt/xdiary/7-deploy/secrets/.env /opt/xdiary/secrets/.env; do
      [ -f "$f" ] || continue
      for k in "${svc^^}_DB_PASSWORD" "POSTGRES_PASSWORD" "PGPASSWORD"; do
        v=$(grep -E "^${k}=" "$f" | head -1 | cut -d= -f2-)
        [ -n "${v:-}" ] && { pwd_="$v"; break 2; }
      done
    done
  fi
  echo "  $svc: db=$db user=$usr host=$host port=${port:-5432}"

  # psql внутри контейнера БД; пароль — через PGPASSWORD, чтобы не светить в ps
  psql_run() {
    podman exec -e "PGPASSWORD=$pwd_" -e "PGSSLMODE=disable" "$PG" \
      psql -h 127.0.0.1 -p "${port:-5432}" -U "$usr" -d "$db" -v ON_ERROR_STOP=1 -tAc "$1" 2>&1
  }

  echo "    история ДО:"
  psql_run "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '2026110517%';" | sed 's/^/      /'
  echo "    таблицы в схеме:"
  psql_run "SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('journal_defaults','journal_read','partners_directory','partners_read');" | sed 's/^/      /'

  if [ "$DRY" = "1" ]; then echo "    (dry-run: INSERT не выполняю)"; continue; fi

  for m in $MIGS; do
    out=$(psql_run "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\",\"ProductVersion\") VALUES ('$m','8.0.11') ON CONFLICT (\"MigrationId\") DO NOTHING;")
    echo "    INSERT $m → ${out:-ok}"
  done

  echo "    история ПОСЛЕ:"
  psql_run "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '2026110517%';" | sed 's/^/      /'

  echo "    перезапуск $ctr: $(podman restart "$ctr" >/dev/null 2>&1 && echo ok || echo FAILED)"
done

# короткая проба публичных путей (если стенд отвечает)
echo "проба публичных путей:"
for u in /api/v1/catalog /api/v1/journal/records /api/v1/partners; do
  c=$(curl -s -o /dev/null -m 15 -w '%{http_code}' "https://xdiary-vps1.taile0b6ee.ts.net$u" || echo 000)
  echo "  $u -> $c"
done
echo "fix-migrations-history: готово (ожидаем 401 на трёх ручках)"
