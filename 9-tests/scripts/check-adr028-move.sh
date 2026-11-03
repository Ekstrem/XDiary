#!/usr/bin/env bash
# Квитанция раскладки тестов по ADR-028 ред. 3 (§5.4: два прогона — на дефекте и на норме).
#
# Правило ред. 3 (@AR 2026-11-14 06:40 — общий бакет отменён владельцем):
#   (1) любой тест микросервиса — в папке своего сервиса `6-msa/BC-<X>/src/<S>.<Kind>Tests`;
#   (2) тесты фронта — только `9-tests` (браузер, маркер `front-e2e.md`);
#   (3) общих тестовых папок нет: `6-msa/E2E`, `6-msa/ApiTests`, `Contract.<A>_<B>` запрещены.
#
# Режимы:
#   --post (по умолчанию) — приёмка ПОСЛЕ перекладывания: 7 проверок должны быть зелёными.
#   --pre                 — состояние ДО (информационный): общий бакет ещё есть.
#
# Использование:
#   bash 9-tests/scripts/check-adr028-move.sh --post
#   REF=HEAD bash 9-tests/scripts/check-adr028-move.sh --post
#
# Печатает PASS/FAIL по каждому пункту, `::error::` на нарушениях и итог
# `PASS: ADR-028 7/7 | режим --post | ревизия <REF>` — эту строку и присылают в чат.
set -uo pipefail

MODE="post"
case "${1:-}" in
  --post) MODE="post" ;;
  --pre)  MODE="pre" ;;
  "")     MODE="post" ;;
  *) echo "usage: $0 [--post|--pre]" >&2; exit 2 ;;
esac

REF="${REF:-origin/main}"
fail=0

say() { printf '%-62s %s\n' "$1" "$2"; }
ok()  { say "$1" "PASS  ($2)"; }
bad() { say "$1" "FAIL  ($2)"; fail=1; echo "::error::ADR-028: $1 - $2"; }

# Baseline атрибутов: сумма по репозиторию (6-msa + 9-tests) при перекладывании не меняется.
# Измерено @TE на 2026-11-14: Fact 128, StandFact 20, Theory 11, ApiFact/E2EFact 17.
BASE_FACT=128
BASE_STAND=20
BASE_THEORY=11
BASE_API=17

echo "режим: --$MODE | ревизия: $REF"
echo "----"

# 1) В 9-tests не осталось C#-проектов (там только браузерные тесты)
n_csproj=$(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep -c '\.csproj$' || true)
if [ "${n_csproj:-0}" = "0" ]; then
  ok '1) .csproj в 9-tests отсутствуют' '0'
else
  bad '1) .csproj в 9-tests отсутствуют' "$n_csproj - тесты сервисов живут в 6-msa/BC-*"
fi

# 2) Общих тестовых бакетов нет (ADR-028 ред. 3 §3.1)
shared=""
for p in 6-msa/E2E 6-msa/ApiTests; do
  if [ -n "$(git ls-tree -r --name-only "$REF" -- "$p" 2>/dev/null | head -n1)" ]; then
    shared="$shared $p"
  fi
done
contract_out=$(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -E '^6-msa/Contract\.[^/]+/' | head -n5 || true)
if [ -z "$shared" ] && [ -z "$contract_out" ]; then
  ok '2) общих тестовых бакетов нет' 'ни E2E, ни ApiTests, ни Contract.<A>_<B>'
else
  bad '2) общих тестовых бакетов нет' "есть:${shared}${contract_out:+ contract: $contract_out}"
fi

# 3) Каждый *Tests.csproj лежит в 6-msa/BC-<X>/src/... или 6-msa/ApiGateway.Tests/...
stray=""
while IFS= read -r proj; do
  [ -z "$proj" ] && continue
  case "$proj" in
    6-msa/BC-*/src/*) ;;
    6-msa/ApiGateway.Tests/*) ;;
    *) stray="$stray $proj" ;;
  esac
done <<EOF
$(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -E 'Tests?\.csproj$' || true)
EOF
n_proj=$(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -cE 'Tests?\.csproj$' || true)
if [ -z "$stray" ]; then
  ok '3) все тестовые проекты в папках своих сервисов' "$n_proj проектов"
else
  bad '3) все тестовые проекты в папках своих сервисов' "вне зоны:$stray"
fi

# 4) В 9-tests фронтовый набор помечен и имеет браузерную зависимость
marker_ok=0
pkg_ok=0
if [ -n "$(git ls-tree -r --name-only "$REF" -- 9-tests/RnD/front-e2e 2>/dev/null | grep 'front-e2e\.md$' || true)" ]; then marker_ok=1; fi
if git show "$REF:9-tests/RnD/front-e2e/package.json" 2>/dev/null | grep -q 'playwright'; then pkg_ok=1; fi
if [ "$marker_ok" = "1" ] && [ "$pkg_ok" = "1" ]; then
  ok '5) front-e2e: маркер + браузерная зависимость' 'front-e2e.md + playwright'
else
  bad '5) front-e2e: маркер + браузерная зависимость' "маркер=$marker_ok playwright=$pkg_ok"
fi

# 4) Все 7 адресов из карты ADR-028 §3.1 на месте (положительная проверка: не «ничего лишнего»,
#    а «всё нужное приехало»). Ожидание — дословно из таблицы §3.1, не по памяти.
missing4=""
for t in \
  '6-msa/BC-Client/src/Client.ApiTests' \
  '6-msa/BC-Catalog/src/Catalog.ApiTests' \
  '6-msa/BC-DailyMetrics/src/DailyMetrics.ApiTests' \
  '6-msa/BC-Portability/src/Portability.ApiTests' \
  '6-msa/BC-Analytics/src/Analytics.ApiTests' \
  '6-msa/BC-Journal/src/Journal.ApiTests' \
  '6-msa/ApiGateway.Tests/ApiGateway.Tests.csproj'
do
  if [ -z "$(git ls-tree -r --name-only "$REF" -- "$t" 2>/dev/null | head -n1)" ]; then
    missing4="$missing4 ${t#6-msa/}"
  fi
done
if [ -z "$missing4" ]; then
  ok '4) все 7 адресов карты ADR-028 3.1 на месте' '7/7'
else
  bad '4) все 7 адресов карты ADR-028 3.1 на месте' "нет:${missing4}"
fi
# 6) В 9-tests нет посторонних исполняемых файлов (идея @DO 2026-11-14 06:50):
#    разрешены RnD/front-e2e (браузер), reports/, scripts/, README.md, AGENTS.md.
foreign=""
while IFS= read -r f; do
  [ -z "$f" ] && continue
  case "$f" in
    9-tests/RnD/front-e2e/*) continue ;;
    9-tests/reports/*|9-tests/scripts/*) continue ;;
    9-tests/README.md|9-tests/AGENTS.md) continue ;;
    *) foreign="$foreign $f" ;;
  esac
done <<EOF
$(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep -E '\.(js|ps1|sh|cs)$' | grep -v '^9-tests/RnD/front-e2e/' || true)
EOF
if [ -z "$foreign" ]; then
  ok '6) в 9-tests нет посторонних скриптов' '0'
else
  bad '6) в 9-tests нет посторонних скриптов' "лишнее:$(echo "$foreign" | tr '\n' ' ' | cut -c1-170)"
fi

# 6) Атрибуты тестов не потерялись (сумма 6-msa + 9-tests)
count_attr() { # $1 = регулярка имени атрибута
  { git grep -h -c -E "\[$1" "$REF" -- 6-msa 2>/dev/null
    git grep -h -c -E "\[$1" "$REF" -- 9-tests 2>/dev/null
  } | awk '{s+=$1} END {print s+0}'
}
fact_total=$(count_attr 'Fact')
stand_total=$(count_attr 'StandFact')
theory_total=$(count_attr 'Theory')
api_total=$(count_attr 'ApiFact|E2EFact')
detail="Fact=${fact_total}/${BASE_FACT} StandFact=${stand_total}/${BASE_STAND} Theory=${theory_total}/${BASE_THEORY} ApiFact=${api_total}/${BASE_API}"
if [ "$fact_total" -ge "$BASE_FACT" ] && [ "$stand_total" -ge "$BASE_STAND" ] \
   && [ "$theory_total" -ge "$BASE_THEORY" ] && [ "$api_total" -ge "$BASE_API" ]; then
  ok '7) атрибуты тестов не потерялись' "$detail"
else
  bad '7) атрибуты тестов не потерялись' "$detail"
fi

# 7) Старые каталоги в 9-tests удалены (иначе проект существует дважды)
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
  ok '8) старые каталоги в 9-tests удалены' '0 остатков'
else
  bad '8) старые каталоги в 9-tests удалены' "остались:$left"
fi

echo "----"
checks=8
if [ "$fail" -eq 0 ]; then
  echo "PASS: ADR-028 ${checks}/${checks} | режим --$MODE | ревизия $REF"
  exit 0
else
  echo "FAIL: см. ::error:: выше | режим --$MODE | ревизия $REF"
  exit 1
fi
