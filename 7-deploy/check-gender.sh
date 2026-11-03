#!/usr/bin/env bash
# check-gender.sh — факт-проверка канона `gender` на dev (DO-060/DO-053, канон ADR-024 §3).
#
# Почему так: без токена профиль не проверить, а OTP на dev уходит владельцу в Telegram.
# На dev код лежит в БД (`clients."OtpCode"`), поэтому берём его оттуда — это dev-контур,
# не прод. На проде скрипт работать не должен (проверка на hostname).
#
#   ./check-gender.sh            прогнать и напечатать таблицу
set -uo pipefail

B=http://127.0.0.1:8080
PHONE="${PHONE:-+7999$(date +%H%M%S)}"

say() { printf '  %s\n' "$*"; }
PG() { sudo -n podman exec xdiary-pg psql -U client -d client -t -A -c "$1" 2>/dev/null | tr -d '\n'; }

host=$(hostname)
case "$host" in
  *vps1*|*prod*) echo "check-gender: это прод-хост — скрипт только для dev"; exit 2;;
esac

say "телефон: $PHONE"
reg=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' \
      -d "{\"phone\":\"$PHONE\"}" "$B/api/v1/clients")
cid=$(printf '%s' "$reg" | sed -n 's/.*"id"[": ]*"\([0-9a-f-]\{36\}\)".*/\1/p')
[ -n "$cid" ] || { say "не создал клиента: ${reg:0:160}"; exit 1; }
say "clientId: $cid"

code=$(PG "select \"OtpCode\" from clients where \"Id\"='$cid'")
[ -n "$code" ] || { say "нет OtpCode в БД"; exit 1; }
say "код из БД: длина ${#code}"

tok=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' \
      -d "{\"code\":\"$code\"}" "$B/api/v1/clients/$cid/verify" \
      | sed -n 's/.*"accessToken"[": ]*"\([^"]*\)".*/\1/p')
[ -n "$tok" ] || { say "не получил accessToken"; exit 1; }
say "accessToken получен (длина ${#tok})"

printf '\n%-14s %-6s %-8s %s\n' "gender" "HTTP" "DB(male?)" "комментарий"
try() {
  local g="$1" label="$2"
  local body out code_db
  body=$(printf '{"displayName":"Проверка","gender":%s}' "$(printf '%s' "$g" | python3 -c 'import json,sys;print(json.dumps(sys.stdin.read()))')")
  out=$(curl -s -o /dev/null -w '%{http_code}' -m 10 -X POST -H 'Content-Type: application/json' \
        -H "Authorization: Bearer $tok" -d "$body" "$B/api/v1/clients/me/profile")
  code_db=$(PG "select coalesce(\"Gender\",'<null>') from clients where \"Id\"='$cid'")
  printf '%-14s %-6s %-8s %s\n' "$label" "$out" "$code_db" ""
}

try 'Other'      'Other'
try 'other'      'other'
try 'м'          'м'
try ''           'пусто'
try '  male  '   '"  male  "'
try 'Male'       'Male'
try 'female'     'female'

echo ""
say "ожидание по канону: Other/other/м/пусто -> 400; '  male  ' -> 200 и в БД male; Male -> 200 и в БД male; female -> 200 и в БД female"
say "finally: $(PG "select coalesce(\"Gender\",'<null>') from clients where \"Id\"='$cid'")"
