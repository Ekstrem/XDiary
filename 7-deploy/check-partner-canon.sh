#!/usr/bin/env bash
# check-partner-canon.sh — факт-проверка канона gender у ПАРТНЁРА на dev/prod.
#
# Почему так, а не «POST /partners с gender»: по контракту `RegisterPartnerCommand`
# принимает только `Name` и `ClientId` — поля `gender` там НЕТ, поэтому любое значение
# игнорируется и отвечает 200 (это и был мой ложный вывод 08:00). Канон живёт во втором
# маршруте: `POST /partners/{id}/profile` → `UpdatePartnerProfileCommand(Photo, Gender, Notes)`,
# где `GenderEnumValidator.Normalize` + `IsSatisfiedBy` → `Fail` для мусора.
#
#   ./check-partner-canon.sh          # dev (OTP из БД)
#   PROD=1 ./check-partner-canon.sh   # прод (тот же путь, если БД доступна)
set -uo pipefail

B="${B:-http://127.0.0.1:8080}"
# rootless (dev/prod под deploy) сначала без sudo; sudo — фолбэк для rootful-контура
PG() { podman exec xdiary-pg psql -U client -d client -t -A -c "$1" 2>/dev/null || sudo -n podman exec xdiary-pg psql -U client -d client -t -A -c "$1" 2>/dev/null; }
say() { printf '  %s\n' "$*"; }
jget() { grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -1 | sed 's/.*:[[:space:]]*"//; s/"$//'; }

# клиент для токена
PHONE="+7999$(printf '%06d' $((RANDOM * 10 + RANDOM % 10)))"
reg=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d "{\"phone\":\"$PHONE\"}" "$B/api/v1/clients")
cid=$(printf '%s' "$reg" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
[ -n "$cid" ] || { say "клиент не создан: $(printf '%s' "$reg" | cut -c1-100)"; exit 1; }
otp=$(PG "select \"OtpCode\" from clients where \"Id\"='$cid'")
tok=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d "{\"code\":\"$otp\"}" \
      "$B/api/v1/clients/$cid/verify" | jget accessToken)
[ -n "$tok" ] || { say "нет токена (verify)"; exit 1; }
say "клиент ${cid:0:8}…, токен: да"

# 1) партнёр: только имя (gender в этой команде нет)
pid=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $tok" \
      -d '{"name":"Проверка канона"}' "$B/api/v1/partners" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
[ -n "$pid" ] || { say "партнёр не создан"; exit 1; }
say "партнёр ${pid:0:8}…"

printf '\n%-30s %-6s %-10s %s\n' "POST /partners/{id}/profile" "HTTP" "в ответе" "ожидание"
try() {
  local g="$1" expect="$2"
  local body out code canon
  body=$(python3 -c 'import json,sys;print(json.dumps({"photo":"","gender":sys.argv[1],"notes":""}))' "$g")
  out=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -H "Authorization: Bearer $tok" \
        -d "$body" "$B/api/v1/partners/$pid/profile")
  code=$(curl -s -o /dev/null -w '%{http_code}' -m 10 -X POST -H 'Content-Type: application/json' \
        -H "Authorization: Bearer $tok" -d "$body" "$B/api/v1/partners/$pid/profile")
  canon=$(printf '%s' "$out" | grep -o '"gender":"[^"]*"' | head -1 | cut -d'"' -f4)
  printf '%-30s %-6s %-10s %s\n' "gender=[$g]" "$code" "${canon:-—}" "$expect"
}

try 'Male'    '200 и canon male'
try '  male  ' '200 и canon male'
try 'female'  '200 и canon female'
try 'Other'   '400'
try 'м'       '400'
try ''        '400'
