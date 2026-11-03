#!/usr/bin/env bash
# Проверка скоупа журнала и владельца записи — те же пять строк, что в приёмке TE-038.
#
# Зачем скрипт, а не «прогоню тест»: на ПРОДЕ получить client-JWT сам тестировщик не может —
# OTP идёт через @xdayry_bot, тестовый канал на проде выключен (и это правильно).
# Поэтому токены передаются в окружении В МОМЕНТ запуска, в репозитории их нет (§6).
#
# Использование (на сервере или где есть сетевой доступ):
#   BASE_URL=https://<прод-хост> \
#   TOKEN_A=<client-JWT клиента A> \
#   TOKEN_B=<client-JWT клиента B, второй клиент> \
#     bash 9-tests/scripts/check-journal-scope.sh
#
# Что печатает: коды по каждой строке + вердикт «PASS/FAIL» по каждой. Токены не печатает.
# Ожидания (ADR-026 / BE-053 / BE-065):
#   1) POST /api/v1/journal/records        → 200 и clientId в ответе НЕ null и = владельцу
#   2) GET  /api/v1/journal/records (A)    → 200 и содержит id только что созданной записи
#   3) GET  /api/v1/journal/records/{id}   → 200 (своя)
#   4) GET  /api/v1/journal/records/{id}   → 403 токеном B (чужая)
#   5) GET  /api/v1/journal/records        → 401 без токена
set -uo pipefail

BASE_URL="${BASE_URL:-}"
TOKEN_A="${TOKEN_A:-}"
TOKEN_B="${TOKEN_B:-}"

if [ -z "$BASE_URL" ] || [ -z "$TOKEN_A" ]; then
  echo "usage: BASE_URL=https://host TOKEN_A=<jwt> [TOKEN_B=<jwt второго клиента>] bash $0" >&2
  echo "       токены НЕ печатаются и в репозиторий не попадают" >&2
  exit 2
fi

fail=0
say() { printf '%-58s %s\n' "$1" "$2"; }
bad() { printf '%-58s %s\n' "$1" "$2"; fail=1; }

# --- 1) создать запись -------------------------------------------------------
resp=$(curl -sS -m 30 -w '\n%{http_code}' -X POST "$BASE_URL/api/v1/journal/records" \
        -H "Authorization: Bearer $TOKEN_A" 2>/dev/null || true)
code=$(printf '%s' "$resp" | tail -n1)
body=$(printf '%s' "$resp" | sed '$d')
id=$(printf '%s' "$body" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -n1)
owner=$(printf '%s' "$body" | sed -n 's/.*"clientId":"\([^"]*\)".*/\1/p' | head -n1)

if [ "$code" = "200" ]; then say "1) POST /journal/records" "200"; else bad "1) POST /journal/records" "$code"; fi
if [ -n "$owner" ]; then say "   clientId в ответе" "не null"; else bad "   clientId в ответе" "null/нет"; fi
if [ -n "$id" ]; then say "   id записи получен" "да"; else bad "   id записи получен" "нет"; fi

# --- 2) список A содержит созданную -----------------------------------------
list=$(curl -sS -m 30 -w '\n%{http_code}' "$BASE_URL/api/v1/journal/records" \
        -H "Authorization: Bearer $TOKEN_A" 2>/dev/null || true)
lcode=$(printf '%s' "$list" | tail -n1)
lbody=$(printf '%s' "$list" | sed '$d')
if [ "$lcode" = "200" ]; then say "2) GET /journal/records (A)" "200"; else bad "2) GET /journal/records (A)" "$lcode"; fi
if [ -n "$id" ] && printf '%s' "$lbody" | grep -q "$id"; then
  say "   своя запись в списке" "есть"
else
  bad "   своя запись в списке" "НЕТ"
fi

# --- 3) своя по id ----------------------------------------------------------
own=$(curl -sS -m 30 -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/journal/records/$id" \
       -H "Authorization: Bearer $TOKEN_A" 2>/dev/null || true)
if [ "$own" = "200" ]; then say "3) GET своей записи по id" "200"; else bad "3) GET своей записи по id" "$own"; fi

# --- 4) чужая по id ---------------------------------------------------------
if [ -n "$TOKEN_B" ]; then
  foreign=$(curl -sS -m 30 -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/journal/records/$id" \
             -H "Authorization: Bearer $TOKEN_B" 2>/dev/null || true)
  if [ "$foreign" = "403" ]; then say "4) GET чужой записи (токен B)" "403"; else bad "4) GET чужой записи (токен B)" "$foreign (ожидали 403)"; fi
else
  say "4) GET чужой записи" "пропущено: TOKEN_B не задан"
fi

# --- 5) аноним --------------------------------------------------------------
anon=$(curl -sS -m 30 -o /dev/null -w '%{http_code}' "$BASE_URL/api/v1/journal/records" 2>/dev/null || true)
if [ "$anon" = "401" ]; then say "5) GET /journal/records без токена" "401"; else bad "5) GET /journal/records без токена" "$anon (ожидали 401)"; fi

echo "----"
if [ "$fail" -eq 0 ]; then
  echo "PASS: 5/5 строк скоупа журнала"
  exit 0
else
  echo "FAIL: см. строки выше (данные не печатались, токены не печатались)"
  exit 1
fi
