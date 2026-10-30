#!/usr/bin/env bash
# Вариант B (решение TL 2026-10-31 03:00): прогон E2E НА VPS-1 против шлюза на localhost:8080.
# Требует: .NET 8 SDK на VPS-1 (ставит DevOps) и запущенный стек (12 контейнеров).
#
# Использование (на VPS-1, из корня репозитория):
#   ./9-tests/E2E/run-e2e-vps.sh
# Переопределить PIN:
#   XDIARY_E2E_PIN=4321 ./9-tests/E2E/run-e2e-vps.sh
set -euo pipefail

export XDIARY_E2E_BASE_URL="${XDIARY_E2E_BASE_URL:-http://localhost:8080}"
export XDIARY_E2E_PIN="${XDIARY_E2E_PIN:-1234}"

echo "== 1/3 проверка шлюза: $XDIARY_E2E_BASE_URL/health =="
if command -v curl >/dev/null 2>&1; then
  curl -fsS "$XDIARY_E2E_BASE_URL/health" && echo "OK"
else
  echo "curl не найден — пропускаю проверку health"
fi

echo "== 2/3 dotnet =="
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: .NET 8 SDK не установлен. DevOps: apt-get install dotnet-sdk-8.0" >&2
  exit 1
fi
dotnet --version

echo "== 3/3 E2E (TE-007..009) =="
PROJ="$(cd "$(dirname "$0")" && pwd)/E2E.Tests/E2E.IntegrationTests.csproj"
dotnet test "$PROJ" --verbosity normal

echo
echo "== нагрузка (TE-010..011) — отдельной командой, нужен токен =="
echo "  1) TOKEN=\$(curl -fsS -X POST \$XDIARY_E2E_BASE_URL/api/v1/privacy/token -H 'Content-Type: application/json' -d '{\"pin\":\"'\"\$XDIARY_E2E_PIN\"'\"}' | sed -E 's/.*\"accessToken\":\"([^\"]+)\".*/\\1/')"
echo "  2) k6 run -e XDIARY_E2E_BASE_URL=\$XDIARY_E2E_BASE_URL -e XDIARY_E2E_TOKEN=\$TOKEN 9-tests/Load/load.js"
echo "  (или PowerShell: ./9-tests/Load/load.ps1 -BaseUrl \$XDIARY_E2E_BASE_URL -Token \$TOKEN -Minutes 10)"
