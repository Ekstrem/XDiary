#!/usr/bin/env bash
# Квитанция «фронт перешёл на профиль E7» — три машинных признака @FE 2026-11-12 11:30.
#
# Зачем: закрыть вопрос «0.88 вышел» фактом, а не датой и не пересказом.
# Скрипт читает только git (сети не требует), печатает PASS/FAIL по каждому признаку.
#
# Использование:
#   bash 9-tests/scripts/check-front-088.sh <sha-или-ref>     # по умолчанию origin/main
#
# Признаки (все обязательны для 0.88+):
#   1) 8-front/js/api.js  содержит "clients/me/profile"   (метод profile вместо linkPartner)
#   2) 8-front/js/app.js  НЕ содержит "linkedPartnerId"   (гейт онбординга больше не на партнёре)
#   3) 8-front/js/app.js  НЕ содержит "linkPartner"       (метод и шаг удалены целиком)
#
# На 0.87 ожидаемо: (1) FAIL, (2) FAIL, (3) FAIL — это норма для старого образа.
set -uo pipefail

REF="${1:-origin/main}"
API="8-front/js/api.js"
APP="8-front/js/app.js"

fail=0
say() { printf '%-52s %s\n' "$1" "$2"; }
ok()  { say "$1" "PASS"; }
bad() { say "$1" "FAIL — $2"; fail=1; }

if ! git rev-parse --verify --quiet "$REF" >/dev/null; then
  echo "ref не найден: $REF" >&2
  exit 2
fi

echo "ref: $REF ($(git rev-parse --short "$REF"))"
echo "----"

n_profile=$(git show "$REF:$API" 2>/dev/null | grep -c "clients/me/profile" || true)
if [ "${n_profile:-0}" -ge 1 ]; then
  ok "1) $API содержит clients/me/profile ($n_profile)"
else
  bad "1) $API содержит clients/me/profile" "0 — фронт ещё не перешёл на профиль"
fi

n_linked=$(git show "$REF:$APP" 2>/dev/null | grep -c "linkedPartnerId" || true)
if [ "${n_linked:-0}" -eq 0 ]; then
  ok "2) $APP без linkedPartnerId"
else
  bad "2) $APP без linkedPartnerId" "$n_linked вхождений — гейт онбординга ещё на партнёре"
fi

n_link=$(git show "$REF:$APP" 2>/dev/null | grep -c "linkPartner" || true)
if [ "${n_link:-0}" -eq 0 ]; then
  ok "3) $APP без linkPartner"
else
  bad "3) $APP без linkPartner" "$n_link вхождений — шаг/метод ещё не удалены"
fi

version=$(git show "$REF:$APP" 2>/dev/null | grep -o 'APP_VERSION *= *"[^"]*"' | head -n1 | sed 's/.*"\(.*\)"/\1/')
echo "----"
echo "APP_VERSION: ${version:-не найден}"

if [ "$fail" -eq 0 ]; then
  echo "PASS: фронт соответствует канону E7 (профиль вместо партнёра)"
  exit 0
else
  echo "FAIL: фронт ещё не соответствует канону E7 (для <0.88 это ожидаемо)"
  exit 1
fi
