#!/usr/bin/env bash
# Сброс PIN на dev-стенде (VPS-2) в 1234 — server-side (ADR-019/020).
# Использование (на VPS-2, пользователь ubuntu):
#   bash reset-dev-pin.sh
set -euo pipefail

# 1. Удаляем текущий PIN-credential (в т.ч. «застрявший» после упавшего change-pin).
sudo podman exec xdiary-pg psql -U postgres -d privacy -c 'DELETE FROM pin_credential;' >/dev/null

# 2. Ставим 1234 (первичная установка — CurrentPin не требуется).
curl -s -X POST http://127.0.0.1:8080/api/v1/privacy/pin/set \
  -H 'Content-Type: application/json' -d '{"pin":"1234"}' \
  -o /dev/null -w 'PIN reset to 1234 (set=%{http_code})\n'
