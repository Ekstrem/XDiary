#!/usr/bin/env bash
# Квитанция переноса тестов по ADR-028 (§5.4: два прогона — на дефекте и на норме).
#
# Кто использует: @BE (переносил `git mv`), @TE (принимает), @TL (вердикт). Скрипт только
# читает git — ничего не меняет и не пушит.
#
# Два режима (@AR: «двухрежимная форма»):
#   --post    (по умолчанию) приёмка ПОСЛЕ переноса: 0 .csproj в 9-tests, все целевые пути в 6-msa,
#             front-e2e на месте, старые каталоги удалены, sln не ссылается на переехавшие проекты,
#             запрещённой папки Contract.<A>_<B> вне E2E нет, атрибуты не потеряны.
#   --pre     состояние ДО переноса (информационный): .csproj в 9-tests ещё есть — режим нужен,
#             чтобы показать «на дефекте гейт красный».
#
# Использование:
#   bash 9-tests/scripts/check-adr028-move.sh --post         # по origin/main
#   REF=HEAD bash 9-tests/scripts/check-adr028-move.sh --post # до пуша, по своему коммиту
#   bash 9-tests/scripts/check-adr028-move.sh --pre
#
# Печатает PASS/FAIL по каждому пункту, ошибки `::error::`, итог `PASS: ADR-028 N/N` и строку
# с проверенной ревизией (REF) — её и надо присылать в чат.
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

say() { printf '%-58s %s\n' "$1" "$2"; }
ok()  { say "$1" "PASS  ($2)"; }
bad() { say "$1" "FAIL  ($2)"; fail=1; echo "::error::ADR-028: $1 — $2"; }

# Baseline считается по СУММЕ 6-msa + 9-tests и при переносе не меняется.
# Значения одинаковы в обоих режимах (перенос не создаёт и не удаляет тесты).
# ВАЖНО: это ИЗМЕРЕННЫЕ числа на проверяемой ревизии, а не «ожидаемые по плану» —
# команда воспроизведения: git grep -h -c -E '\[Fact' REF -- 6-msa 9-tests | awk '{s+=$1} END{print s}'
BASE_FACT=128
BASE_STAND=20
BASE_THEORY=11
BASE_E2E=17

echo "режим: --$MODE | ревизия: $REF"
echo "----"

# 1) .csproj в 9-tests
n_csproj=$(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep -c '\.csproj$' || true)
if [ "$MODE" = "post" ]; then
  if [ "$n_csproj" = "0" ]; then
    ok '1) .csproj в 9-tests отсутствуют' '0'
  else
    bad '1) .csproj в 9-tests отсутствуют' "$n_csproj — перенос не завершён"
  fi
else
  if [ "${n_csproj:-0}" -gt 0 ]; then
    ok '1) --pre: .csproj в 9-tests ещё есть (ожидаемо)' "$n_csproj"
  else
    bad '1) --pre: .csproj в 9-tests ещё есть (ожидаемо)' '0 — перенос уже выполнен, --pre неприменим'
  fi
fi

# 2) Все 8 целевых путей существуют в 6-msa
missing=""
# ADR-028 редакция 2 (@AR 2026-11-13 16:05): папка `Contract.<A>_<B>` вне `E2E` ЗАПРЕЩЕНА,
# межсервисные black-box сценарии — в 6-msa/E2E/<pair>, шлюз — в 6-msa/ApiGateway.Tests.
for p in \
  '6-msa/E2E' \
  '6-msa/E2E/Contract.Client_Journal' \
  '6-msa/E2E/Partners_Catalog' \
  '6-msa/E2E/DailyMetrics_SomaticSeries' \
  '6-msa/E2E/Gateway_Portability' \
  '6-msa/E2E/Analytics_Privacy' \
  '6-msa/ApiGateway.Tests' \
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
#    она не меняется, а при потере файла — падает. Baseline ИЗМЕРЕН на 2026-11-14 @TE:
#    Fact 128, StandFact 20, Theory 11, E2EFact 17 (см. комментарий у BASE_FACT).
count_attr() { # $1 = атрибут
  { git grep -h -c -E "\[$1" "$REF" -- 6-msa 2>/dev/null
    git grep -h -c -E "\[$1" "$REF" -- 9-tests 2>/dev/null
  } | awk '{s+=$1} END {print s+0}'
}
fact_total=$(count_attr 'Fact')
stand_total=$(count_attr 'StandFact')
theory_total=$(count_attr 'Theory')
e2e_total=$(count_attr 'E2EFact')

detail="Fact=${fact_total}/${BASE_FACT} StandFact=${stand_total}/${BASE_STAND} Theory=${theory_total}/${BASE_THEORY} E2EFact=${e2e_total}/${BASE_E2E}"
if [ "$fact_total" -ge "$BASE_FACT" ] && [ "$stand_total" -ge "$BASE_STAND" ] \
   && [ "$theory_total" -ge "$BASE_THEORY" ] && [ "$e2e_total" -ge "$BASE_E2E" ]; then
  ok '5) атрибуты тестов не потерялись' "$detail"
else
  bad '5) атрибуты тестов не потерялись' "$detail"
fi

# 6) Запрещённая форма не появилась: `6-msa/Contract.<A>_<B>` вне `E2E` (комплаенс ADR-028 п.5)
forbidden=$(git ls-tree -r --name-only "$REF" -- 6-msa 2>/dev/null | grep -E '^6-msa/Contract\.[^/]+/' | head -n3 || true)
if [ -z "$forbidden" ]; then
  ok '6) нет папки Contract.<A>_<B> вне E2E' '0'
else
  bad '6) нет папки Contract.<A>_<B> вне E2E' "$forbidden"
fi

# 7) Солюшен 9-tests не ссылается на переехавшие проекты (иначе он сломан:
#    `dotnet build 9-tests/XDiary.Tests.sln` не находит файлы). Проверяются .sln в 9-tests.
stale=""
for s in $(git ls-tree -r --name-only "$REF" -- 9-tests 2>/dev/null | grep '\.sln$' || true); do
  hits=$(git show "$REF:$s" 2>/dev/null \
    | grep -c -E 'Contract\.ClientJournal|Partners\.Catalog|Somatic\.Daily|Portability\.Gateway|Analytics\.Privacy|Gateways\.Api|E2E\\E2E' || true)
  [ "${hits:-0}" -gt 0 ] && stale="$stale $s($hits)"
done
if [ -z "$stale" ]; then
  ok '7) .sln в 9-tests не ссылается на переехавшие проекты' '0 ссылок'
else
  bad '7) .sln в 9-tests не ссылается на переехавшие проекты' "сломан:$stale"
fi

echo "----"
checks=7
if [ "$fail" -eq 0 ]; then
  echo "PASS: ADR-028 ${checks}/${checks} | режим --$MODE | ревизия $REF"
  exit 0
else
  echo "FAIL: см. ::error:: выше | режим --$MODE | ревизия $REF"
  exit 1
fi
