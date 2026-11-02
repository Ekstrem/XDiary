#!/usr/bin/env bash
# Сброс PIN на dev-стенде (VPS-2) в 1234 — безопасный (DO-024).
# Раньше делал DELETE FROM pin_credential (без WHERE) — стирал PIN человека,
# работающего на стенде. Теперь человеческий PIN не трогаем:
#   - PIN уже 1234        → no-op (идемпотентно);
#   - PIN человеческий    → отказ (exit 1), прогон E2E/smoke пропускается;
#   - PIN отсутствует     → первичная установка 1234 (CurrentPin не требуется).
set -euo pipefail

BASE="http://127.0.0.1:8080/api/v1/privacy"

# 1. PIN уже 1234? (токен с pin=1234 вернёт 200)
code=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/token" \
  -H 'Content-Type: application/json' -d '{"pin":"1234"}')
if [ "$code" = "200" ]; then
  echo "PIN уже 1234 — сброс не нужен (идемпотентно)."
  exit 0
fi

# 2. Есть ли вообще PIN (человеческий)?
has=$(curl -s "$BASE/pin/has")
if [ "$has" = "true" ]; then
  echo "⚠️  Обнаружен человеческий PIN (не 1234) — НЕ затираю (DO-024)." >&2
  echo "    Прогон E2E/smoke пропущен: стенд занят человеком." >&2
  exit 1
fi

# 3. PIN нет — первичная установка 1234.
curl -s -X POST "$BASE/pin/set" \
  -H 'Content-Type: application/json' -d '{"pin":"1234"}' \
  -o /dev/null -w 'PIN set to 1234 (set=%{http_code})\n'
