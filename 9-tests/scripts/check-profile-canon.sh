#!/usr/bin/env bash
# Квитанция канона профиля (`POST /clients/me/profile`) — ADR-024 §3 / BE-063.
#
# Зачем: @TL 2026-11-12 14:10 — «канон живёт только в контрактных тестах; нужен
# артефакт по форме: скрипт + контур + автор + дата». Форма та же, что у
# `check-journal-scope.sh`: токен передаётся в окружении В МОМЕНТ запуска,
# в репозитории его нет, в вывод он не попадает (§6).
#
# Использование:
#   BASE_URL=https://<хост> TOKEN=<client-JWT> bash 9-tests/scripts/check-profile-canon.sh
#
# Ожидания (ADR-024 §3): нормализация Trim+lower, словарь male|female,
# сохранение КАНОНИЧЕСКОГО значения; всё прочее — 400.
#   1) "Male"      -> 200, в ответе/`me` — male
#   2) "  male  "  -> 200, в `me` — male
#   3) "female"    -> 200, в `me` — female
#   4) "Other"     -> 400        (неизвестное значение отклонено)
#   5) "м"         -> 400        (не английская буква)
#   6) пустой пол  -> 400
# Плюс контроль доступа: POST без токена -> 401.
# ВАЖНО про побочный эффект (@TL 2026-11-13 02:21, поправка к формулировке «побочных эффектов нет»):
# прогон СОЗДАЁТ нового тестового клиента и ПЕРЕЗАПИСЫВАЕТ его профиль (gender меняется между шагами:
# male -> male -> female -> ...), поэтому корректная формулировка — «влияет только на тестового клиента».
# На боевых данных и на других клиентах эффекта нет.
set -uo pipefail

BASE_URL="${BASE_URL:-}"
TOKEN="${TOKEN:-}"

if [ -z "$BASE_URL" ] || [ -z "$TOKEN" ]; then
  echo "usage: BASE_URL=https://host TOKEN=<client-JWT> bash $0" >&2
  echo "       токен НЕ печатается и в репозиторий не попадает" >&2
  exit 2
fi

fail=0
say() { printf '%-46s %s\n' "$1" "$2"; }
ok()  { say "$1" "PASS  ($2)"; }
bad() { say "$1" "FAIL  ($2)"; fail=1; }

post_profile() { # $1 = gender, $2 = displayName
  curl -sS -m 30 -w '\n%{http_code}' -X POST "$BASE_URL/api/v1/clients/me/profile" \
    -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
    -d "{\"displayName\":\"$2\",\"gender\":\"$1\"}" 2>/dev/null || true
}
get_me() {
  curl -sS -m 30 "$BASE_URL/api/v1/clients/me" -H "Authorization: Bearer $TOKEN" 2>/dev/null || true
}
canon_of_me() {
  get_me | sed -n 's/.*"gender":"\([^"]*\)".*/\1/p' | head -n1
}

check_ok() { # $1 = метка, $2 = gender, $3 = ожидаемый канон
  local resp code me
  resp=$(post_profile "$2" "TE-canon")
  code=$(printf '%s' "$resp" | tail -n1)
  me=$(canon_of_me)
  if [ "$code" = "200" ] && [ "$me" = "$3" ]; then
    ok "$1" "200, в me: $me"
  else
    bad "$1" "код=$code, в me: '${me:-нет}' (ожидали 200 и $3)"
  fi
}

check_reject() { # $1 = метка, $2 = gender
  local resp code
  resp=$(post_profile "$2" "TE-canon")
  code=$(printf '%s' "$resp" | tail -n1)
  if [ "$code" = "400" ]; then
    ok "$1" "400"
  else
    bad "$1" "код=$code (ожидали 400)"
  fi
}

echo "контур: $BASE_URL"
echo "----"
check_ok     '1) gender "Male"'      'Male'      'male'
check_ok     '2) gender "  male  "'  '  male  '  'male'
check_ok     '3) gender "female"'    'female'    'female'
check_reject '4) gender "Other"'     'Other'
check_reject '5) gender "м"'         'м'
check_reject '6) пустой gender'      ''

anon=$(curl -sS -m 30 -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/api/v1/clients/me/profile" \
        -H 'Content-Type: application/json' -d '{"displayName":"x","gender":"male"}' 2>/dev/null || true)
if [ "$anon" = "401" ]; then ok '7) без токена' '401'; else bad '7) без токена' "код=$anon (ожидали 401)"; fi

echo "----"
if [ "$fail" -eq 0 ]; then
  echo "PASS: канон профиля 7/7 (6 значений + контроль доступа)"
  exit 0
else
  echo "FAIL: см. строки выше (значения не печатались, токен не печатался)"
  exit 1
fi
