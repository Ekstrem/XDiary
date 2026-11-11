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

## Приёмка после выкатки `partners` (`BE-069`) — что именно прислать

Одна выкатка разблокирует `BE-069`, `TE-050`, `FE-041`, `DO-079`. Чтобы вердикты были мгновенными,
нужны **пять строк**, каждая — с числом или кодом (не «работает»):

```bash
# 1. сервис жив и отвечает бизнес-кодом, а не 500 (изнутри контура)
podman exec xdiary-client curl -sS -o /dev/null -w '%{http_code}\n'   -H "Authorization: Bearer <JWT>" http://xdiary-partners:8080/api/v1/partners
# ожидание: 200 (было 500). Без токена — 401, это не показатель.

# 2. контракт мягкого удаления
gh workflow run test-partner-softdelete.yml --ref main
# ожидание в логе: Total tests: 4 / Passed: 4 / Failed: 0

# 3. E2E, офлайн-сценарий визарда (поведенческая приёмка TE-054)
podman run --rm --network xdiary -v "$PWD:/w" -w /w/9-tests/RnD/front-e2e \
  -e XDIARY_E2E_BASE_URL=https://xdiary-vps2.taile0b6ee.ts.net -e XDIARY_E2E_PIN=1234 \
  -e XDIARY_READPATH_OFFLINE=1 mcr.microsoft.com/playwright:v1.64.1-noble \
  node node_modules/@playwright/test/cli.js test tests/wizard-offline.spec.js
# ожидание: 1 passed; в логе спека печатает «версия измеряемого фронта = 0.9x»
# (если версия < 0.93 — красный объясним известным дефектом 0.92, см. отчёт)

# 4. lock соответствует git после выкатки
python tools/gen_images_lock.py --check     # ожидание rc=0

# 5. условие «чистая БД» (@AR): сервис стартовал без ручных CREATE TABLE
podman exec xdiary-journal psql -U <user> -d journal -c '\dt'   # ожидание: 3 read-таблицы
```

**Почему именно так:** `TE-050` принимается по `4 passed` **плюс** `/partners` → 200 изнутри
(иначе тесты снова упадут `500`); `TE-054` — только по поведению (`wizard-offline`), а не по
source-level гейту; `lock rc=0` нужен, чтобы «зелёный E2E» относился к выкаченной версии, а не к
предыдущей; `\dt` — чтобы `4 passed` не оказался «работает там, где таблицы создали руками».

> **Осторожно, предсказанный второй отказ (проверено по коду 2026-11-05):** в `BC-Partners`
> `ReadDbContext` объявляет только `Entity<PartnerEntity>`, а регистрация просит
> `AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>(…, null)`; объявления
> `Entity<PartnersReadModel>` нет. Поэтому шаг 1 может дать не `200`, а `500` с
> `Cannot create a DbSet for 'PartnersReadModel'`. Это **не** ошибка выкатки и **не** `lock`:
> нужен маппинг read-модели (класс `BE-068`, BC `Partners`).

> **Обновление 2026-11-05 (`e7a9bd30`):** предсказанный `500 Cannot create a DbSet` **снят** — маппинг
> `Entity<PartnersReadModel>` → `partners_read` и DDL добавлены. Но приёмка шага 1 не сводится к `200`:
> read-путь читает **отдельную** таблицу `partners_read`, которую заполняет **асинхронная** проекция.
> Поэтому нужно `200` **и непустой список после создания партнёра** (либо `SELECT count(*) FROM partners_read > 0`);
> `200 []` — это не успех, а «проекция не догнала или пишет не туда». И отдельно: партнёры, созданные
> **до** выкатки, в `partners_read` без бэкфилла не появятся.

---

## Поправка `2026-11-11 22:15`: команды, которые реально существуют

Прежняя запись ссылалась на `test-partner-softdelete.yml`, а такого workflow в `origin` **нет** —
это подтвердил и @DO (`chat/do.md 2026-11-11 20:14`: «в `.github/workflows` только `ci-partners.yml` и …»).
Проверено чтением `origin` (`git show origin/main:.github/workflows/<файл>`), ниже — рабочие команды.

| Что нужно | Существующий workflow | Dispatch-входы |
|---|---|---|
| контракт партнёров (`TE-050`) | `ci-backend.yml` (джоб `test` → `dotnet test` по проектам) | нет |
| браузер, точечный набор (`TE-053`, `TE-054`, `TE-104`) | `front-e2e-repeat.yml` | `specs`, `repeat`, `cleanup`, `workers` |
| браузер, полный набор | `ci-front.yml` (джоб `front-e2e`; пушем идёт только `spec-gates`) | нет |
| `Partners` отдельно | `ci-partners.yml` | нет |

Несуществующие имена (`front-e2e.yml`, `test-partner-softdelete.yml`) в инструкциях **не использовать**.

```bash
# 1) TE-050 — контракт мягкого удаления партнёра (4 теста Journal.ApiTests)
gh workflow run ci-backend.yml --ref main
gh run list --workflow=ci-backend.yml -L 1
# в логе джоба `test` искать: Passed! … 4 passed (PartnerSoftDeleteTests)
# 0 тестов найдено — НЕ успех («прогон без шагов — не прогон»)

# 2) браузерные квитанции одним запуском: TE-053, TE-054, TE-104
gh workflow run front-e2e-repeat.yml --ref main   -f specs='tests/pin-fallback.spec.js tests/wizard-offline.spec.js tests/startup-api-error.spec.js'   -f repeat=1 -f cleanup=yes -f workers=1
gh run list --workflow=front-e2e-repeat.yml -L 1
# в квитанции: номер прогона + `REF`-коммит; для TE-054 нужен ещё негативный контроль
# (спека `wizard-offline` включает `XDIARY_READPATH_OFFLINE=1`, он уже выставлен в ci-front.yml:62
#  и в самом front-e2e-repeat.yml:47)

# 3) полный браузерный набор (если нужен общий прогон, а не точечный)
gh workflow run ci-front.yml --ref main
```

Ожидаемые квитанции по карточкам:

- `TE-050` — `dotnet test` `rc=0`, строка `4 passed`, и `REF` коммита;
- `TE-053` — `pin-fallback` прошёл, `REF`;
- `TE-054` — `wizard-offline` прошёл (флаг уже в CI), `REF`;
- `TE-104` — `startup-api-error` прошёл (сплэш уходит за 2 с при `500`), `REF`;
- `BE-044`, `DO-079` — те же прогоны дают номер, который они просят.

### Третий путь: ночное расписание (уже настроено) — бесплатный REF

Проверено по `origin` (`.github/workflows/ci-front.yml`):

- `:34-35` — `schedule: - cron: '0 4 * * *'` (04:00 UTC);
- `:48-50` — `front-e2e: needs: spec-gates` и `if: github.event_name != 'push' && github.event_name != 'pull_request'`
  ⇒ **ни один пуш не даёт браузерного `REF`**, входы — `workflow_dispatch` и `schedule` (это `DO-103`, P0);
- `:54-56` — `concurrency` с `cancel-in-progress: false` ⇒ пуш **не прерывает** идущий браузерный прогон.

**Следствие для шести карточек** (`TE-050`, `TE-053`, `TE-054`, `TE-104`, `BE-044`, `DO-079`): если ночной
прогон завершился сам, `REF` уже есть и dispatch не нужен — достаточно взять номер из списка прогонов
(`gh run list --workflow=ci-front.yml -L 5`). Dispatch — ускоритель, а не единственный путь.

**Что важно проверить в квитанции ночного прогона:** `spec-gates` прошёл (иначе `front-e2e` не стартует
из-за `needs`), прогон **завершился сам** (не `cancelled`), и в логе исполнены нужные спеки
(`pin-fallback`, `wizard-offline`, `startup-api-error`).
