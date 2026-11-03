#!/usr/bin/env bash
# check-pii-scope.sh — факт-проверка ADR-026 / BE-064 на dev:
#   список клиентов удалён; `GET /clients/{id}` — только «себя», чужой → 403 (не 404!).
#
# Почему так: без токена профиль не проверить, а OTP на dev уходит владельцу в Telegram.
# На dev код лежит в БД (`clients."OtpCode"`), берём оттуда. Прод — отказ по hostname.
set -uo pipefail

B=http://127.0.0.1:8080
PG() { sudo -n podman exec xdiary-pg psql -U client -d client -t -A -c "$1" 2>/dev/null | tr -d '\n'; }
say() { printf '  %s\n' "$*"; }

host=$(hostname)
case "$host" in *vps1*|*prod*) echo "check-pii-scope: это прод-хост — скрипт только для dev"; exit 2;; esac

make_client() {   # -> "id token"
  local phone="+7999$(date +%H%M%S)$1"
  local reg cid code tok
  reg=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d "{\"phone\":\"$phone\"}" "$B/api/v1/clients")
  cid=$(printf '%s' "$reg" | sed -n 's/.*"id"[": ]*"\([0-9a-f-]\{36\}\)".*/\1/p')
  [ -n "$cid" ] || { echo "ERR"; return 1; }
  code=$(PG "select \"OtpCode\" from clients where \"Id\"='$cid'")
  tok=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d "{\"code\":\"$code\"}" \
        "$B/api/v1/clients/$cid/verify" | sed -n 's/.*"accessToken"[": ]*"\([^"]*\)".*/\1/p')
  echo "$cid $tok"
}

read -r AID ATOK <<<"$(make_client 1)"
read -r BID BTOK <<<"$(make_client 2)"
[ -n "${AID:-}" ] && [ -n "${BID:-}" ] || { say "не создал двух клиентов"; exit 1; }
say "A=${AID:0:8}… B=${BID:0:8}…"

code() { curl -s -o /dev/null -w '%{http_code}' -m 10 "$@"; }

printf '\n%-42s %-6s %s\n' "запрос" "HTTP" "ожидание"
printf '%-42s %-6s %s\n' "GET /api/v1/clients (без токена)" \
  "$(code "$B/api/v1/clients")" "401/404 (список удалён)"
printf '%-42s %-6s %s\n' "GET /api/v1/clients (с токеном A)" \
  "$(code -H "Authorization: Bearer $ATOK" "$B/api/v1/clients")" "401/404 (списка нет)"
printf '%-42s %-6s %s\n' "GET /clients/A  (токен A)" \
  "$(code -H "Authorization: Bearer $ATOK" "$B/api/v1/clients/$AID")" "200 (своя)"
printf '%-42s %-6s %s\n' "GET /clients/B  (токен A)" \
  "$(code -H "Authorization: Bearer $ATOK" "$B/api/v1/clients/$BID")" "403 (чужая; 404 = нет проверки владельца)"
printf '%-42s %-6s %s\n' "GET /clients/A  (без токена)" \
  "$(code "$B/api/v1/clients/$AID")" "401"
