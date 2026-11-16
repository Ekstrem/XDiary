#!/usr/bin/env bash
set -euo pipefail

# XDiary — бэкап PostgreSQL (VPS-1, co-located) → VPS-2 (бэкап-таргет). DO-006 / ADR-016 / DO-089.
# Устанавливается на VPS-1 (пользователь deploy), запускается cron ежедневно.
# Требует ssh-ключ ~/.ssh/id_ed25519_vps2_backup, авторизованный на VPS-2 (ubuntu).
#
# ВАЖНО (найдено 2026-11-05, DO-089): прежняя версия делала
#   `podman exec xdiary-pg pg_dumpall -U postgres | gzip > file`
# и **теряла данные молча**: `podman exec` обрезает поток большого вывода, при этом
# код возврата конвейера = rc от `gzip`, то есть 0. Замер на проде:
#   через пайп               — 172K / 3 базы (в другом прогоне 596K / 8 баз), rc=0
#   внутри контейнера в файл — 720K / 11 баз (полный кластер)
# Поэтому дамп делается В КОНТЕЙНЕРЕ, забирается `podman cp`, и полнота проверяется
# ДО отправки (число баз == число `CREATE DATABASE`). Неполный дамп НЕ отправляется
# и завершается ненулевым кодом — «тихого успеха» больше нет.

BACKUP_DIR="${BACKUP_DIR:-/home/deploy/backups}"
REMOTE="${REMOTE:-ubuntu@77.91.73.67:/home/ubuntu/backups/}"
SSH_KEY="${SSH_KEY:-/home/deploy/.ssh/id_ed25519_vps2_backup}"
KEEP_DAYS="${KEEP_DAYS:-14}"
CT="${PG_CONTAINER:-xdiary-pg}"

TS="$(date +%F_%H%M%S)"
TMP_IN_CONTAINER="/tmp/xdiary-dump-$TS.sql"
PLAIN="$BACKUP_DIR/xdiary-all-$TS.sql"
OUT="$BACKUP_DIR/xdiary-all-$TS.sql.gz"
mkdir -p "$BACKUP_DIR"
umask 077

cleanup() {
  podman exec "$CT" rm -f "$TMP_IN_CONTAINER" >/dev/null 2>&1 || true
  rm -f "$PLAIN"
}
trap cleanup EXIT

echo "==> дамп внутри контейнера ($CT) -> $TMP_IN_CONTAINER"
podman exec "$CT" sh -c "pg_dumpall -U postgres > $TMP_IN_CONTAINER"

echo "==> проверка полноты ДО отправки"
EXPECTED=$(podman exec "$CT" psql -U postgres -Atc \
  "select count(*) from pg_database where not datistemplate and datname <> 'postgres';")
ACTUAL=$(podman exec "$CT" sh -c "grep -c '^CREATE DATABASE' $TMP_IN_CONTAINER || true")
if [ "$EXPECTED" != "$ACTUAL" ]; then
  echo "FAIL: дамп неполный — баз в кластере $EXPECTED, CREATE DATABASE в дампе $ACTUAL" >&2
  exit 2
fi
echo "    баз: $EXPECTED, CREATE DATABASE: $ACTUAL — совпало"

echo "==> забираю и сжимаю -> $OUT"
podman cp "$CT:$TMP_IN_CONTAINER" "$PLAIN"
gzip -c "$PLAIN" > "$OUT"
SIZE=$(stat -c%s "$OUT")
if [ "$SIZE" -lt 1024 ]; then
  echo "FAIL: сжатый дамп подозрительно мал ($SIZE байт)" >&2
  exit 2
fi
echo "    размер: $(du -h "$OUT" | cut -f1)"

echo "==> отправка на VPS-2"
scp -P "${SSH_PORT:-8237}" -q -i "$SSH_KEY" -o StrictHostKeyChecking=no -o ConnectTimeout=30 "$OUT" "$REMOTE"
REMOTE_HOST="${REMOTE%%:*}"
REMOTE_DIR="${REMOTE#*:}"
REMOTE_FILE="$REMOTE_DIR/$(basename "$OUT")"
if ! ssh -p "${SSH_PORT:-8237}" -i "$SSH_KEY" -o StrictHostKeyChecking=no -o ConnectTimeout=30 "$REMOTE_HOST" \
     "test -s '$REMOTE_FILE'"; then
  echo "FAIL: на VPS-2 файла нет или он пустой: $REMOTE_FILE" >&2
  exit 2
fi
echo "    на VPS-2: $REMOTE_FILE"

echo "==> ротация (локально и на VPS-2, старше ${KEEP_DAYS} дн.)"
find "$BACKUP_DIR" -name 'xdiary-all-*.sql.gz' -mtime +"$KEEP_DAYS" -delete
ssh -p "${SSH_PORT:-8237}" -i "$SSH_KEY" -o StrictHostKeyChecking=no -o ConnectTimeout=30 "$REMOTE_HOST" \
  "find '$REMOTE_DIR' -name 'xdiary-all-*.sql.gz' -mtime +$KEEP_DAYS -delete" || true

echo "OK: $(basename "$OUT") ($(du -h "$OUT" | cut -f1)), баз $ACTUAL, VPS-2, ротация ${KEEP_DAYS}д оба контура"
