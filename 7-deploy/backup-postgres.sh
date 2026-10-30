#!/usr/bin/env bash
set -euo pipefail

# XDiary — бэкап PostgreSQL (VPS-2) + restore-тест. DO-006.
# Устанавливается в /usr/local/bin/xdiary-pg-backup из provision-vps2.sh,
# запускается cron ежедневно.
#
# Ручной запуск:
#   sudo xdiary-pg-backup                  # обычный бэкап
#   sudo RESTORE_TEST=1 xdiary-pg-backup   # + проверка восстановления

: "${BACKUP_DIR:=/backups/postgres}"
: "${KEEP_DAYS:=14}"
TS="$(date +%F_%H%M%S)"

mkdir -p "$BACKUP_DIR"
umask 077

echo "==> pg_dumpall (полный кластер) -> $BACKUP_DIR/xdiary-all-$TS.sql.gz"
sudo -u postgres pg_dumpall --clean --if-exists \
  | gzip > "$BACKUP_DIR/xdiary-all-$TS.sql.gz"

echo "==> ротация (старше ${KEEP_DAYS} дн.)"
find "$BACKUP_DIR" -name 'xdiary-all-*.sql.gz' -mtime "+${KEEP_DAYS}" -delete

# Проверка восстановления (опционально). Кастомный дамп одной БД -> pg_restore
# в тестовую БД. Весь блок выполняется от postgres (единые права на файл в /tmp).
if [[ "${RESTORE_TEST:-0}" == "1" ]]; then
  TEST_DB="xdiary_restore_test"
  DUMP="/tmp/xdiary-journal-$TS.dump"
  echo "==> restore-тест БД 'journal' -> $TEST_DB"
  sudo -u postgres bash -ec "
    pg_dump -Fc --clean --if-exists journal > '$DUMP'
    psql -v ON_ERROR_STOP=1 -c 'DROP DATABASE IF EXISTS $TEST_DB;' postgres >/dev/null
    psql -v ON_ERROR_STOP=1 -c 'CREATE DATABASE $TEST_DB;' postgres >/dev/null
    pg_restore --clean --if-exists --no-owner --dbname='$TEST_DB' '$DUMP'
    psql -v ON_ERROR_STOP=1 -c 'DROP DATABASE $TEST_DB;' postgres >/dev/null
    rm -f '$DUMP'
  "
  echo "OK: restore-тест прошёл без ошибок"
fi

echo "OK: $BACKUP_DIR/xdiary-all-$TS.sql.gz"
