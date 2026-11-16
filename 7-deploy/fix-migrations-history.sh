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

conn_for() { # $1 = имя контейнера сервиса → печатает строку подключения внутри сети
  podman exec "$1" printenv ConnectionStrings__CommandDb 2>/dev/null
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
  [ -n "$db" ] && [ -n "$usr" ] || { echo "  $svc: в строке подключения нет Database/Username" >&2; continue; }
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
