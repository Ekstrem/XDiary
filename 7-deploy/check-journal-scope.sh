#!/usr/bin/env bash
# check-journal-scope.sh — факт-проверка скоупа журнала по владельцу (BE-053 / TE-039 Point3b).
#
# Зачем: `GET /journal/records` должен отдавать только свои записи, чужая по id — 403.
# Регресс 2026-11-10: `ClientId` не проставлялся при создании → запись с null-владельцем,
# список пуст, своя запись → 403. Скрипт снимает «до/после» одним прогоном.
#
# Только dev (берёт OTP из БД); на прод-хосте отказывается работать.
set -uo pipefail

B=http://127.0.0.1:8080
# Две БД: клиенты (`client`) и журнал (`journal`) — это разные базы, и это легко
# перепутать: запрос к неверной БД молча возвращает пустоту, а не ошибку.
PG()  { sudo -n podman exec xdiary-pg psql -U client  -d client  -t -A -c "$1" 2>/dev/null | tr -d '\n'; }
PGJ() { sudo -n podman exec xdiary-pg psql -U journal -d journal -t -A -c "$1" 2>/dev/null | tr -d '\n'; }
say() { printf '  %s\n' "$*"; }
# Извлечение значения по ключу: `grep -o` устойчивее sed-подстановки — sed с жадным
# `.*` на длинном JSON с несколькими строками возвращал пусто, и это выглядело как
# «токена нет» (хотя verify отдавал accessToken).
jget() { grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | head -1 | sed 's/.*:[[:space:]]*"//; s/"$//'; }
jnum() { sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\([0-9-]*\).*/\1/p"; }

host=$(hostname)
case "$host" in *vps1*|*prod*) echo "check-journal-scope: только dev"; exit 2;; esac

mk_client() {  # $1 = суффикс телефона -> "id token"
  local phone="+7999$(date +%H%M%S)$1" reg cid code tok
  reg=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d "{\"phone\":\"$phone\"}" "$B/api/v1/clients")
  cid=$(printf '%s' "$reg" | jget id)
  [ -n "$cid" ] || { echo "ERR ERR"; return 1; }
  code=$(PG "select \"OtpCode\" from clients where \"Id\"='$cid'")
  tok=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' -d "{\"code\":\"$code\"}" \
        "$B/api/v1/clients/$cid/verify" | jget accessToken)
  echo "$cid $tok"
}

read -r AID ATOK <<<"$(mk_client 1)"
read -r BID BTOK <<<"$(mk_client 2)"
[ -n "${AID:-}" ] && [ -n "${BID:-}" ] || { say "не создал клиентов"; exit 1; }
say "A=${AID:0:8}… B=${BID:0:8}…"

# A создаёт запись
created=$(curl -s -m 10 -X POST -H 'Content-Type: application/json' \
          -H "Authorization: Bearer $ATOK" -d '{"place":"Нальчик"}' "$B/api/v1/journal/records")
rid=$(printf '%s' "$created" | jget id)
say "создана запись: ${rid:-НЕТ}"
say "clientId в ответе: $(printf '%s' "$created" | grep -o '"clientId"[^,}]*' | head -1)"

# в БД: проставлен ли владелец
if [ -n "${rid:-}" ]; then
  say "clientId в БД: $(PGJ "select coalesce(\"ClientId\"::text,'<null>') from records where \"Id\"='$rid'")"
fi

printf '\n%-44s %-6s %s\n' "запрос" "HTTP" "ожидание"
printf '%-44s %-6s %s\n' "GET /journal/records (токен A)" \
  "$(curl -s -o /tmp/ja.json -w '%{http_code}' -m 10 -H "Authorization: Bearer $ATOK" "$B/api/v1/journal/records")" "200 и содержит id своей"
printf '%-44s %-6s %s\n' "GET /journal/records/{своя} (токен A)" \
  "$(curl -s -o /dev/null -w '%{http_code}' -m 10 -H "Authorization: Bearer $ATOK" "$B/api/v1/journal/records/$rid")" "200"
printf '%-44s %-6s %s\n' "GET /journal/records/{своя} (токен B)" \
  "$(curl -s -o /dev/null -w '%{http_code}' -m 10 -H "Authorization: Bearer $BTOK" "$B/api/v1/journal/records/$rid")" "403"
printf '%-44s %-6s %s\n' "GET /journal/records (без токена)" \
  "$(curl -s -o /dev/null -w '%{http_code}' -m 10 "$B/api/v1/journal/records")" "401"

if [ -n "${rid:-}" ] && [ -f /tmp/ja.json ]; then
  if grep -q "$rid" /tmp/ja.json; then say "своя запись в списке: ДА"; else say "своя запись в списке: НЕТ (регресс: clientId не проставлен)"; fi
fi

say "владелец в журнале: $(PGJ "select count(*) filter (where \"ClientId\" is null) || ' из ' || count(*) || ' записей без владельца' from records")"
