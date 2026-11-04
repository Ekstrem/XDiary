#!/usr/bin/env bash
# inotify-limits.sh — поднять лимиты inotify на VPS (DO-050 / DO-076).
#
# Почему: каждый .NET-сервис в rootless-podman поднимает FileSystemWatcher'ы
# (конфиг + `reloadOnChange`), и на проде лимит `fs.inotify.max_user_instances`
# (по умолчанию 16384) был съеден целиком — замер: **занято 24641** при лимите 16384.
# Симптом в логах сервиса:
#   Unhandled exception. System.IO.IOException: The configured user limit (16384) on the
#   number of inotify instances has been reached, or the per-process limit ...
#     at Microsoft.Extensions.FileProviders.Physical.PhysicalFilesWatcher...
# Последствие: контейнер падает на старте → рестарт-петля (`Restarts` десятки тысяч),
# сервис недоступен, а выглядит это как «код сломан».
#
# Идемпотентно: пишет /etc/sysctl.d/99-xdiary-inotify.conf и применяет.
#   sudo -n bash 7-deploy/inotify-limits.sh
set -euo pipefail

CONF=/etc/sysctl.d/99-xdiary-inotify.conf
BEFORE_I=$(sysctl -n fs.inotify.max_user_instances)
BEFORE_W=$(sysctl -n fs.inotify.max_user_watches)
USED=$(find /proc/*/fd -lname 'anon_inode:inotify' 2>/dev/null | wc -l)

echo "до: instances=$BEFORE_I watches=$BEFORE_W, занято инстансов=$USED"

sudo -n tee "$CONF" >/dev/null <<'CONF'
# XDiary: rootless podman + .NET FileSystemWatcher на каждый контейнер съедают inotify
# instances. Дефолт 16384 приводил к падению сервисов с
# "The configured user limit on the number of inotify instances has been reached"
# и рестарт-петлям. Настроено 2026-11-04 (DO-076).
fs.inotify.max_user_instances = 65536
fs.inotify.max_user_watches = 524288
CONF
sudo -n sysctl -p "$CONF" >/dev/null

echo "после: instances=$(sysctl -n fs.inotify.max_user_instances) watches=$(sysctl -n fs.inotify.max_user_watches)"
echo "проверка: лимит выше занятого ($(sysctl -n fs.inotify.max_user_instances) > $USED)"
