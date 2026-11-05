# Прогон тестов вне песочницы агента TE (для `DO-079`)

Песочница TE физически не может выполнить две вещи — это граница среды, а не отказ
(подтверждено командами, см. `9-tests/README.md` → «Пределы песочницы агента»):

| Что нужно | Почему не здесь | Точная ошибка |
|---|---|---|
`dotnet test` (контракт партнёров, `TE-050`) | `testhost` не может стартовать | сборка rc=0, затем `System.ComponentModel.Win32Exception (5)` в `Process.GetProcessHandle` |
Playwright E2E (`TE-053`, `TE-055`, `FE-040`-регресс) | браузеры не устанавливаются | `npx playwright install chromium` → `EPERM mkdir %LOCALAPPDATA%\ms-playwright`; с `PLAYWRIGHT_BROWSERS_PATH` в `%TEMP%` → **`spawn EPERM`** (загрузчик форкает процесс с пайп-stdio, пайпы запрещены) |

Ниже — команды, готовые к исполнению в CI или в контейнере. Оба прогона обязаны назвать
**точку замера** (`base`) и **версию инструмента/коммита**, иначе результат неотличим от
«зелено на пустоте».

## 1. Контракт мягкого удаления партнёра (`TE-050`)

Что должно быть выкачено: `partners` на новом образе (иначе тесты упадут `502`/таймаутом).

```bash
# вариант А — workflow (предпочтительно)
gh workflow run test-partner-softdelete.yml --ref main
gh run list --workflow=test-partner-softdelete.yml -L 1
# ожидание в логе: Passed! … 4 passed
```

```bash
# вариант Б — контейнер с доступом к контуру
podman run --rm --network xdiary -v "$PWD:/w" -w /w \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test 6-msa/BC-Journal/src/Journal.ApiTests --logger "console;verbosity=normal"
# ожидание: Passed! 4 теста (PartnerSoftDeleteTests: удаление, идемпотентность, 404, HistoryStaysResolvable)
```

**Критерий приёмки:** номер прогона (или `dotnet test` rc=0 + строка `Passed!`) и `4 passed`.
Если 0 тестов найдено — это **не** успех (класс «прогон без шагов — не прогон»).

## 2. Браузерные E2E (`TE-053`, `TE-055`, регресс `FE-040`)

Важно: `use.baseURL = http://127.0.0.1:4173`, статику поднимает `webServer` (`node serve.js`) —
то есть тест проверяет **свой** фронт из `8-front/`, а API берёт из `XDIARY_E2E_BASE_URL`.

```bash
podman run --rm --network xdiary -v "$PWD:/w" -w /w/9-tests/RnD/front-e2e \
  -e XDIARY_E2E_BASE_URL=https://xdiary-vps2.taile0b6ee.ts.net \
  -e XDIARY_E2E_PIN="<PIN стенда>" \
  -e XDIARY_READPATH_OFFLINE=1 \
  mcr.microsoft.com/playwright:v1.64.1-noble \
  node node_modules/@playwright/test/cli.js test --reporter=list
# ожидание: 19 спеков, в конце «N passed / M skipped / K failed»
```

Точечный прогон для моих карточек:

```bash
node node_modules/@playwright/test/cli.js test tests/pin-fallback.spec.js tests/remove-pin.spec.js
node node_modules/@playwright/test/cli.js test tests/trends.spec.js tests/csv.spec.js   # регресс FE-040
```

**Критерий приёмки:** `passed/failed/skipped` по файлу + `XDIARY_E2E_BASE_URL` в строке
результата + `XDIARY_READPATH_OFFLINE=1` (без него `wizard-offline.spec.js` не проверяет
различимость ошибки чтения и «данных нет» — это условие `TE-054`).

Пропуск `pin-fallback.spec.js` допустим **только** если стенд без PIN, и это должно быть
видно в логе как внешнее условие (`probe: POST /api/v1/privacy/token {pin:""} → 200`),
а не как «skipped» без причины.

## Что считается результатом

Строка `→ @TL` с: (1) точкой замера (`base`), (2) коммитом/версией образа, (3) числом
`passed/failed/skipped`, (4) ссылкой на прогон. «Не получилось» без причины не принимается —
причина обязана быть названа так же, как в таблице выше (код ошибки + что именно запрещено).
