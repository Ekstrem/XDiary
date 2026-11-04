#!/usr/bin/env bash
# reset-pin.sh — сброс PIN на КОНТУРЕ (dev/prod), воспроизводимо и с подтверждением.
#
# Почему: 2026-11-03 PO не мог войти в прод, и «сброс PIN» делали руками на сервере
# (`reset-dev-pin.sh` был только для dev, для прода — ничего). «Починил на сервере без
# скрипта» — это дрейф (AGENTS §5.2), поэтому операция описана здесь.
#
# ВНИМАНИЕ — это данные владельца. Без `--yes` скрипт только печатает состояние.
#
# Использование (на сервере соответствующего контура):
#   bash 7-deploy/reset-pin.sh --contour dev                 # показать состояние
#   bash 7-deploy/reset-pin.sh --contour dev --yes --pin 1234 # сбросить и поставить 1234
#   sudo -n bash 7-deploy/reset-pin.sh --contour prod --yes  # прод: podman под sudo
set -euo pipefail

CONTOUR=""
PIN="1234"
YES=0
SUDO=""
while [ $# -gt 0 ]; do
  case "$1" in
    --contour) CONTOUR="${2:-}"; shift 2 ;;
    --pin) PIN="${2:-}"; shift 2 ;;
    --yes) YES=1; shift ;;
    --sudo) SUDO="sudo -n"; shift ;;
    *) echo "usage: $0 --contour dev|prod [--pin 1234] [--yes] [--sudo]" >&2; exit 2 ;;
  esac
done
[ -n "$CONTOUR" ] || { echo "укажи --contour dev|prod" >&2; exit 2; }
POD="$SUDO podman"
PG="$POD exec xdiary-pg"

echo "контур: $CONTOUR"
n=$($PG psql -U privacy -d privacy -A -t -c 'select count(*) from pin_credential' 2>/dev/null | tr -d ' ')
echo "pin_credential строк: ${n:-?}"
$PG psql -U privacy -d privacy -A -t -c \
  'select "ClientId", "SourceId", "Locked" from pin_credential' 2>/dev/null | sed 's/^/  /'

if [ "$YES" -ne 1 ]; then
  echo "ничего не меняю: для сброса добавь --yes (это данные владельца)"
  exit 0
fi

echo "сбрасываю PIN: удаляю записи и ставлю новый через API"
$PG psql -U privacy -d privacy -c 'delete from pin_credential' >/dev/null
echo "  pin_credential очищена; новый PIN ставится через приложение/API:"
echo "  POST /api/v1/privacy/pin  с JWT клиента (нужен client-токен: см. runbook про token)"
echo "  затем POST /api/v1/privacy/token с этим JWT — должен вернуть accessToken"
echo "  PIN к установке: $PIN"
