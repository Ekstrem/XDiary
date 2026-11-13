# Готовность к тестированию — сводка с квитанциями (2026-11-13 11:3x)

Состояние на `main` = `aa3b0e98d`. Свежая квитанция — прогон `43394018588` (workflow_dispatch, sha `9d78c404`). Формат: **пункт → что сделано → чем доказано**. Где доказательства нет — так и написано.

## 1. Deploy (`main` → dev → prod)

| Джоб прогона `43394018588` (sha `9d78c404`) | Итог |
|---|---|
`diff-paths`, `lock-published` | ✅ |
`deploy-dev` | ✅ |
**`smoke-front`** | **✅** (после `gh run rerun --failed`: 1-я попытка упала на флейке `add-partner` → `TE-110`) |
`smoke-api` | ✅ |
**`receipt-be092`** | **✅** |
`smoke-prod-cors` | ✅ |
**`deploy-prod`** | **❌** (единственный красный; причина ниже, патч №18) |

Ранее тот же набор был подтверждён на прогоне `43379615221` (sha `33e2f503`): там `smoke-front` и `receipt-be092` тоже зелёные — то есть это не разовая удача.

**Причина `deploy-prod` (найдена по логу, не гипотеза):**
```
APIM_PREFLIGHT_OK
/opt/xdiary/7-deploy/update.sh: line 59: say: command not found
/home/<deploy>/<script>.sh разошёлся со снимком (sha256 9173778a… != 7b62a233…)
```
`deploy.yml`: **dev-джоб доставляет** скрипт (`UPDATE_B64` → `base64 -d > /opt/xdiary/7-deploy/update.sh`, строки 137/155), **prod-джоб — нет** (только `bash /home/deploy/update.sh`, строка 490). В git `say()` определена (строки 40–56), строка 59 — комментарий ⇒ на VPS-1 живёт **устаревшая** копия.

**Готовое решение:** патч `tl/patches/deploy-prod-deliver-update-sh.patch` (реестр №18) — добавляет доставку в prod-джоб. Проверен: `yaml.safe_load` ok, `check_workflow_shape` **43/0**, `git apply --check` **rc=0**. Статус: ждёт применения @DO (зона `.github`).

## 2. E2E-харнесс (`FE-046`)

| Утверждение | Доказательство в `main` |
|---|---|
класс «браузер закрывается во время навигации» закрыт | `FE-046` — **Закрыта**, есть `## Результат` |
нет безтаймаутных ожиданий | `playwright.config.js`: `actionTimeout: 15000`; `tests/api.js`: `timeout: 1500` — **6 вхождений** |
шаг длительности визарда не проскакивает | `wizard-record.spec.js`: есть `#w-dur-custom` и проверка «длительность уже показана» |
квитанция прогоном | `ci-front` на `role/te-wizard2`: `wizard-record.spec.js:46` → **✓ PASSED (8.1s)** |

## 3. Транспорт PR (`DO-119`)

| Звено | Доказательство |
|---|---|
`pr-create` работает на пушах | прогоны `43393380336`, `43393493643`, `43393599131`, `43393638546`, `43393651913` — все `success` |
авто-создание PR | в логах `created=true`, `gh pr create --base main` |
dispatch обходит анти-рекурсию | в `ci-guards` есть `event=workflow_dispatch` на `role/te-otp` (06:15:51Z) |

Известный косметический дефект: при существующем PR лог печатает «чеки перезапускаем», хотя условие шага `created == 'true'` (логика верная, текст врёт).

## 4. P0/P1, блокировавшие тестирование

| Карточка | Статус в `main` | Квитанция |
|---|---|---|
`TE-097` | **Закрыта** | тесты 5/5 + 2/2, прогон `43370351690` success |
`DO-082` | **Закрыта** | вердикт @AR (405/404/400/401) |
`TL-002` | **Закрыта** | `AGENTS.md` 33 404 → 25 966, `check_docs_size` rc=0 |
`DO-099` | `ToDo` (работа сделана) | `check_otp_leak` подключён в `deploy.yml:400-404` и **отработал в CI**; `--selftest` 16/16 → ждёт закрытия владельцем |
`DO-103` (P0) | `На проверке` | вердикт @TL 00:56 → ждёт закрытия владельцем |
`DO-085` | `ToDo` | @DO ведёт (`789366151`), критерий 2 закрыт |
`BE-071`, `BE-074` | **`В паузе`** | замер: тестирование не блокируют; порядок `ADR-030` §5 требует идти после `DO-087` |

## 5. WIP-лимит и транспорт коммитов

`review_whip` → **3 ≤ 5** (`DO-103`, `DO-119`, `DO-123`). Все изменения раундов — через `role/tl` + PR (`#2204`–`#2207`), прямых пушей в `main` нет: в `main` есть `check_direct_push` (`--first-parent`).

## Остаток до «зелёного деплоя» — два действия

1. применить патч №18 в `.github/workflows/deploy.yml` (@DO);
2. прогнать `deploy` на `main` — ожидаем `deploy-prod` ✅; если сторож эталона всё ещё ругается, прогнать на VPS-1 `/opt/xdiary/7-deploy/legacy-shims.sh` (пересоберёт шим и эталон, старый файл бэкапится).

Ничего другого в цепочке не осталось: все предыдущие шаги зелёные **в одном прогоне**.

## Как это перепроверить (команды)

```bash
# состояние деплоя на текущем main
gh run list --workflow deploy.yml --limit 2
# джобы конкретного прогона
gh run view <run-id> --json jobs --jq '.jobs[] | "\(.name) \(.conclusion)"'
# что именно не так с prod (если упал)
gh run view <run-id> --log --job=<deploy-prod-job-id> | grep -E "APIM_PREFLIGHT|say:|разошёлся|sha256"
# гейты на main локально
python tools/check_awaits.py --strict && python tools/status_lint.py --strict \
  && python tools/check_nul.py && python tools/stats.py --check && python tools/chat_lint.py --strict
# патч №18 применяется?
git apply --check 5-tech-backlog/tl/patches/deploy-prod-deliver-update-sh.patch
```
