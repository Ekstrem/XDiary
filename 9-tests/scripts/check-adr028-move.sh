#!/usr/bin/env bash
# Квитанция переноса тестов по ADR-028 (§5.4: проверка после переноса).
#
# Кто использует: @BE (выполняет `git mv` по §2 карты `9-tests/reports/ADR-028-move-plan.md`)
# и @TE (принимает результат). Скрипт только читает git — ничего не меняет и не пушит.
#
# Использование:
#   bash 9-tests/scripts/check-adr028-move.sh            # проверить origin/main
#   REF=HEAD bash 9-tests/scripts/check-adr028-move.sh   # проверить свой локальный коммит до пуша
#
# Печатает PASS/FAIL по каждому пункту и итог `PASS: ADR-028 5/5` или `FAIL`.
set -uo pipefail

REF="${REF:-origin/main}"
fail=0

say() { printf '%-58s %s\n' "$1" "$2"; }
ok()  { say "$1" "PASS  ($2)"; }
bad() { say "$1" "FAIL  ($2)"; fail=1; }

# 1) В 9-tests не осталось C#-проектов (по ADR-028 они переезжают в 6-msa)
n_csproj=$(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep -c '\.csproj$' || true)
if [ "$n_csproj" = "0" ]; then
  ok '1) .csproj в 9-tests отсутствуют' '0'
else
  bad '1) .csproj в 9-tests отсутствуют' "$n_csproj — перенос не завершён"
fi

# 2) Все 8 целевых путей существуют в 6-msa
missing=""
for p in \
  '6-msa/Contract.Client_Journal' \
  '6-msa/Contract.Catalog_Partners' \
  '6-msa/Contract.DailyMetrics_SomaticSeries' \
  '6-msa/Contract.Gateway_Portability' \
  '6-msa/Contract.Analytics_Privacy' \
  '6-msa/Gateways' \
  '6-msa/E2E' \
  '6-msa/BC-Journal/src/Journal.IntegrationTests'
do
  found=$(git ls-tree -r --name-only "$REF" -- "$p" 2>/dev/null | head -n1)
  [ -z "$found" ] && missing="$missing $p"
done
if [ -z "$missing" ]; then
  ok '2) все 8 целевых путей есть в 6-msa' '8/8'
else
  bad '2) все 8 целевых путей есть в 6-msa' "нет:$missing"
fi

# 3) Фронтовые E2E остались (единственное, что требует браузера)
if git cat-file -e "$REF:9-tests/RnD/front-e2e/playwright.config.js" 2>/dev/null; then
  ok '3) 9-tests/RnD/front-e2e на месте' 'playwright.config.js есть'
else
  bad '3) 9-tests/RnD/front-e2e на месте' 'playwright.config.js пропал'
fi

# 4) Старые каталоги в 9-tests удалены (иначе проект существует дважды)
left=""
for d in \
  '9-tests/Contract.ClientJournal' '9-tests/Partners.Catalog' '9-tests/Somatic.Daily' \
  '9-tests/Portability.Gateway' '9-tests/Analytics.Privacy' '9-tests/Gateways' \
  '9-tests/Gateways.Api' '9-tests/Journal' '9-tests/E2E'
do
  if [ -n "$(git ls-tree -r --name-only "$REF" -- "$d" 2>/dev/null | head -n1)" ]; then
    left="$left $d"
  fi
done
if [ -z "$left" ]; then
  ok '4) старые каталоги в 9-tests удалены' '0 остатков'
else
  bad '4) старые каталоги в 9-tests удалены' "остались:$left"
fi

# 5) Число тестов не потерялось. Считаем СУММУ по репозиторию (6-msa + 9-tests): при переносе
#    она не меняется, а при потере файла — падает. Baseline на 2026-11-13 (origin, до переноса):
#    Fact 128 (6-msa 71 + 9-tests 57), StandFact 20, Theory 11 (2 + 9), E2EFact 17.
count_attr() { # $1 = атрибут
  { git grep -h -c -E "\[$1" "$REF" -- 6-msa 2>/dev/null
    git grep -h -c -E "\[$1" "$REF" -- 9-tests 2>/dev/null
  } | awk '{s+=$1} END {print s+0}'
}
fact_total=$(count_attr 'Fact')
stand_total=$(count_attr 'StandFact')
theory_total=$(count_attr 'Theory')
e2e_total=$(count_attr 'E2EFact')

detail="Fact=${fact_total}/128 StandFact=${stand_total}/20 Theory=${theory_total}/11 E2EFact=${e2e_total}/17"
if [ "$fact_total" -ge 128 ] && [ "$stand_total" -ge 20 ] && [ "$theory_total" -ge 11 ] && [ "$e2e_total" -ge 17 ]; then
  ok '5) атрибуты тестов не потерялись' "$detail"
else
  bad '5) атрибуты тестов не потерялись' "$detail"
fi

echo "----"
if [ "$fail" -eq 0 ]; then
  echo "PASS: ADR-028 5/5 (ревизия $REF)"
  exit 0
else
  echo "FAIL: см. строки выше (ревизия $REF)"
  exit 1
fi
