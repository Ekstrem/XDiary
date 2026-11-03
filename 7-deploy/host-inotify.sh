#!/usr/bin/env bash
# host-inotify.sh — лимиты inotify для хостов с .NET-сервисами (DO-072).
#
# Почему: 2026-11-04 прод отдавал 502, `xdiary-api-gateway` падал с `ExitCode=139`, и в
# логе было:
#   System.IO.IOException: The configured user limit (128) on the number of inotify
#   instances has been reached …  at System.IO.FileSystemWatcher.StartRaisingEvents()
#   at Microsoft.Extensions.Configuration.FileConfigurationProvider..ctor
# То есть .NET ставит FileSystemWatcher на `appsettings.json` (reloadOnChange), и каждая
# копия сервиса тратит inotify-инстанс. На хосте с 12–14 контейнерами дефолт 128
# (fs.inotify.max_user_instances) исчерпывается — сервис не стартует, шлюз не поднялся,
# снаружи это выглядит как 502 от Caddy/Funnel, а не как ошибка конфигурации.
#
# Идемпотентно: пишет sysctl.d-файл и применяет. Запуск: `bash 7-deploy/host-inotify.sh`
# (нужен root; в deploy/update вызывается через sudo -n).
set -euo pipefail

INSTANCES="${XDIARY_INOTIFY_INSTANCES:-1024}"
WATCHES="${XDIARY_INOTIFY_WATCHES:-524288}"
CONF=/etc/sysctl.d/99-xdiary-inotify.conf

cur_i=$(sysctl -n fs.inotify.max_user_instances 2>/dev/null || echo 0)
cur_w=$(sysctl -n fs.inotify.max_user_watches 2>/dev/null || echo 0)
echo "host-inotify: сейчас instances=$cur_i watches=$cur_w; целевые $INSTANCES/$WATCHES"

if [ "$cur_i" -ge "$INSTANCES" ] && [ "$cur_w" -ge "$WATCHES" ]; then
  echo "host-inotify: лимиты уже достаточные — ничего не меняю"
  exit 0
fi

printf 'fs.inotify.max_user_instances = %s\nfs.inotify.max_user_watches = %s\n' \
  "$INSTANCES" "$WATCHES" > "$CONF" 2>/dev/null || {
    printf 'fs.inotify.max_user_instances = %s\nfs.inotify.max_user_watches = %s\n' \
      "$INSTANCES" "$WATCHES" | sudo -n tee "$CONF" >/dev/null; }
sysctl -p "$CONF" >/dev/null 2>&1 || sudo -n sysctl -p "$CONF" >/dev/null

echo "host-inotify: применено instances=$(sysctl -n fs.inotify.max_user_instances) watches=$(sysctl -n fs.inotify.max_user_watches)"
echo "host-inotify: файл $CONF переживает перезагрузку"
