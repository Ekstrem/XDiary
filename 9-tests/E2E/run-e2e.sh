#!/usr/bin/env bash
# Прогон E2E против стенда XDiary (TE-007..009).
# Использование:
#   BASE="https://xdiary-vps1.taile0b6ee.ts.net" PIN=1234 ./run-e2e.sh
# Требует: .NET 8 SDK и сетевой доступ к стенду (Tailscale).
set -euo pipefail

: "${BASE:?Задай BASE, напр. https://xdiary-vps1.taile0b6ee.ts.net}"
export XDIARY_E2E_BASE_URL="$BASE"
export XDIARY_E2E_PIN="${PIN:-1234}"

PROJ="$(cd "$(dirname "$0")" && pwd)/E2E.Tests/E2E.IntegrationTests.csproj"
echo "== E2E -> $XDIARY_E2E_BASE_URL (pin=${XDIARY_E2E_PIN}) =="
dotnet test "$PROJ" --verbosity normal
