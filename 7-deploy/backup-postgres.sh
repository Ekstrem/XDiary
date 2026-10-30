#!/usr/bin/env bash
set -euo pipefail

# XDiary — бэкап PostgreSQL (VPS-1, co-located) → VPS-2 (бэкап-таргет). DO-006 / ADR-016.
# Устанавливается на VPS-1 (пользователь deploy), запускается cron ежедневно.
# Требует ssh-ключ ~/.ssh/id_ed25519_vps2_backup, авторизованный на VPS-2 (ubuntu).

BACKUP_DIR="${BACKUP_DIR:-/home/deploy/backups}"
REMOTE="${REMOTE:-ubuntu@77.91.73.67:/home/ubuntu/backups/}"
SSH_KEY="${SSH_KEY:-/home/deploy/.ssh/id_ed25519_vps2_backup}"
KEEP_DAYS="${KEEP_DAYS:-14}"
TS="$(date +%F_%H%M%S)"

mkdir -p "$BACKUP_DIR"
umask 077

echo "==> pg_dumpall (полный кластер, из контейнера xdiary-pg) -> $BACKUP_DIR/xdiary-all-$TS.sql.gz"
podman exec xdiary-pg pg_dumpall -U postgres | gzip > "$BACKUP_DIR/xdiary-all-$TS.sql.gz"

echo "==> отправка на VPS-2 (бэкап-таргет)"
scp -i "$SSH_KEY" -o StrictHostKeyChecking=no -o ConnectTimeout=30 \
  "$BACKUP_DIR/xdiary-all-$TS.sql.gz" "$REMOTE"

echo "==> ротация (локально, старше ${KEEP_DAYS} дн.)"
find "$BACKUP_DIR" -name 'xdiary-all-*.sql.gz' -mtime "+${KEEP_DAYS}" -delete

echo "OK: $BACKUP_DIR/xdiary-all-$TS.sql.gz -> VPS-2"
