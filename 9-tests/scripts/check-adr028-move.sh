#!/usr/bin/env bash
# Приёмка раскладки тестов по ADR-028 ред. 3 (§5.4: два прогона — на дефекте и на норме).
#
# Что проверяется (9 проверок):
#   1) в `9-tests` нет `.csproj` (там только браузерные тесты);
#   2) общих тестовых папок нет: `6-msa/E2E`, `6-msa/ApiTests`, `Contract.<A>_<B>`;
#   3) каждый `*Tests.csproj` — в `6-msa/BC-*/src/…` или `6-msa/ApiGateway.Tests/`;
#   4) все 7 адресов карты ADR-028 §3.1 на месте (положительная проверка);
#   5) `9-tests/RnD/front-e2e`: маркер `front-e2e.md` + `playwright` в `package.json`;
#   6) в `9-tests` нет посторонних исполняемых файлов;
#   7) атрибуты тестов не потерялись (baseline: Fact 128 / StandFact 20 / Theory 11 / ApiFact 17);
#   8) старые каталоги `9-tests` удалены;
#   9) изоляция: в тестовом проекте нет ссылки на чужой BC (кроме `6-msa/Shared/**`) —
#      ни `ProjectReference`, ни `Compile Include`. Добавлено по @TL 2026-11-14 30:10:
#      без этой проверки приёмка давала «8/8» при красной изоляции.
#
# Режимы: --post (по умолчанию) — рабочая приёмка; --pre — состояние до перекладывания.
# Использование:
#   bash 9-tests/scripts/check-adr028-move.sh --post
#   REF=HEAD bash 9-tests/scripts/check-adr028-move.sh --post
#
# Печатает `REF=…` первой строкой и в итоге; при нарушении — `::error::` с путём.
# Внимание: в песочнице @TE bash не запускается (не создаётся signal pipe), поэтому
# там используют Python-двойник `9-tests/scripts/check_adr028_move.py` с теми же 9 проверками.
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

BASE_FACT=128
BASE_STAND=20
BASE_THEORY=11
BASE_API=17

echo "REF=$REF | режим: --$MODE"

if ! git rev-parse --verify "$REF^{commit}" >/dev/null 2>&1; then
  echo "::error::ADR-028: REF не резолвится в этом репозитории - приёмка не выполнена"
  echo "REF=$REF | FAIL: невалидная ревизия | режим --$MODE"
  exit 2
fi

echo "----"

# 1) .csproj в 9-tests
n_csproj=$(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep -c '\.csproj$' || true)
if [ "${n_csproj:-0}" = "0" ]; then
  ok '1) .csproj в 9-tests отсутствуют' '0'
else
  bad '1) .csproj в 9-tests отсутствуют' "$n_csproj - тесты сервисов живут в 6-msa/BC-*"
fi

# 2) общих тестовых бакетов нет
shared=""
for p in 6-msa/E2E 6-msa/ApiTests; do
  if [ -n "$(git ls-tree -r --name-only "$REF" -- "$p" 2>/dev/null | head -n1)" ]; then
    shared="$shared $p"
  fi
done
contract_out=$(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -E '^6-msa/Contract\.[^/]+/' | head -n3 || true)
if [ -z "$shared" ] && [ -z "$contract_out" ]; then
  ok '2) общих тестовых бакетов нет' 'ни E2E, ни ApiTests, ни Contract.<A>_<B>'
else
  for p in $shared; do
    echo "::error::ADR-028 3.1: общая тестовая папка запрещена - $p"
  done
  if [ -n "$contract_out" ]; then
    echo "::error::ADR-028 3.1: каталог Contract.<A>_<B> запрещён"
  fi
  bad '2) общих тестовых бакетов нет' "есть:$shared"
fi

# 3) каждый тестовый проект — в папке своего сервиса
stray=""
n_proj=0
for proj in $(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -E 'Tests?\.csproj$' || true); do
  n_proj=$((n_proj + 1))
  case "$proj" in
    6-msa/BC-*/src/*) ;;
    6-msa/ApiGateway.Tests/*) ;;
    *) stray="$stray $proj" ;;
  esac
done
if [ -z "$stray" ]; then
  ok '3) все тестовые проекты в папках своих сервисов' "$n_proj проектов"
else
  bad '3) все тестовые проекты в папках своих сервисов' "вне зоны:$stray"
fi

# 4) все 7 адресов §3.1 на месте
missing=""
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
    missing="$missing ${t#6-msa/}"
  fi
done
if [ -z "$missing" ]; then
  ok '4) все 7 адресов карты ADR-028 3.1 на месте' '7/7'
else
  bad '4) все 7 адресов карты ADR-028 3.1 на месте' "нет:$missing"
fi

# 5) front-e2e: маркер + браузер
marker_ok=0
pkg_ok=0
if [ -n "$(git ls-tree -r --name-only "$REF" -- 9-tests/RnD/front-e2e 2>/dev/null | grep 'front-e2e\.md$' || true)" ]; then
  marker_ok=1
fi
if git show "$REF:9-tests/RnD/front-e2e/package.json" 2>/dev/null | grep -q 'playwright'; then
  pkg_ok=1
fi
if [ "$marker_ok" = "1" ] && [ "$pkg_ok" = "1" ]; then
  ok '5) front-e2e: маркер + браузерная зависимость' 'front-e2e.md + playwright'
else
  bad '5) front-e2e: маркер + браузерная зависимость' "маркер=$marker_ok playwright=$pkg_ok"
fi

# 6) посторонние исполняемые файлы в 9-tests
foreign=""
for f in $(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep -E '\.(js|ps1|sh|cs)$' | grep -v '^9-tests/RnD/front-e2e/' || true); do
  case "$f" in
    9-tests/reports/*) continue ;;
    9-tests/scripts/*) continue ;;
    *) foreign="$foreign $f" ;;
  esac
done
if [ -z "$foreign" ]; then
  ok '6) в 9-tests нет посторонних скриптов' '0'
else
  bad '6) в 9-tests нет посторонних скриптов' "лишнее:$foreign"
fi

# 7) атрибуты тестов не потерялись
count_attr() {
  { git grep -h -c -E "\[$1" "$REF" -- ':(glob)6-msa/**/*.cs' 2>/dev/null
    git grep -h -c -E "\[$1" "$REF" -- ':(glob)9-tests/**/*.cs' 2>/dev/null
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

# 8) старые каталоги 9-tests
left=""
for d in \
  '9-tests/Contract.ClientJournal' '9-tests/Partners.Catalog' '9-tests/Somatic.Daily' \
  '9-tests/Portability.Gateway' '9-tests/Analytics.Privacy' '9-tests/Gateways' \
  '9-tests/Gateways.Api' '9-tests/Journal' '9-tests/E2E' '9-tests/Load'
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

# 9) изоляция: нет ссылок на чужой BC (кроме 6-msa/Shared/**)
n_viol=0
for proj in $(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -E '^6-msa/BC-[^/]+/src/.*Tests\.csproj$' || true); do
  owner=${proj#6-msa/BC-}
  owner=${owner%%/*}
  incs=$(git show "$REF:$proj" 2>/dev/null | grep -oE 'Include="[^"]+"' | sed 's/Include="//; s/"$//' || true)
  for inc in $incs; do
    case "$inc" in
      *Shared*) continue ;;
    esac
    other=$(printf '%s' "$inc" | grep -oE 'BC-[A-Za-z0-9_]+' | head -n1 | sed 's/^BC-//')
    if [ -z "$other" ]; then
      continue
    fi
    if [ "$other" = "$owner" ]; then
      continue
    fi
    echo "::error::ADR-028 1.1: ссылка на чужой BC в тестах - $proj -> BC-$other"
    n_viol=$((n_viol + 1))
  done
done
if [ "$n_viol" -eq 0 ]; then
  ok '9) изоляция тестов: нет ссылок на чужой BC' '0 (Shared разрешён)'
else
  bad '9) изоляция тестов: нет ссылок на чужой BC' "$n_viol ссылк(и) в проектах"
fi

echo "----"
checks=9
if [ "$fail" -eq 0 ]; then
  echo "REF=$REF | PASS: ADR-028 ${checks}/${checks} | режим --$MODE"
  exit 0
fi
echo "REF=$REF | FAIL: см. ::error:: выше | режим --$MODE"
exit 1
