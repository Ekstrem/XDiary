# 9-tests — рабочая папка тестировщика

Здесь живут автотесты XDiary (интеграционные/E2E/нагрузочные), которые пишет роль **tester**.
Задачи — в `5-tech-backlog/tester` (TE-001…TE-011).

## ⚠️ Имя проекта ≠ имя сервиса

Вывод о том, что покрывает проект, делается **только** по `git grep`/чтению файлов,
**никогда** по названию папки. На этом ошиблись двое за один час:
`9-tests/Gateways` — это **Garmin + AppleHealth** (домены приёма данных), а **не** `ApiGateway`;
маршрутизация шлюза не покрыта нигде.

| Проект | Какие сервисы покрывает |
|---|---|
| `9-tests/Gateways` | `BC-Garmin`, `BC-AppleHealth` — **не** `ApiGateway` |
| `9-tests/Journal` | `BC-Journal` |
| `9-tests/Partners.Catalog` | `BC-Partners`, `BC-Catalog` |
| `9-tests/Somatic.Daily` | `BC-Somatic`, `BC-DailyMetrics` |
| `9-tests/Analytics.Privacy` | `BC-Analytics`, `BC-Privacy` |
| `9-tests/Portability.Gateway` | `BC-Portability` |
| `9-tests/E2E` | весь стек через `ApiGateway` (сквозной) |
| `9-tests/RnD/front-e2e` | PWA `8-front` (Playwright, гоняется `front-e2e`) |

Проверка перед выводом: `git grep -c "<эндпоинт>" -- 9-tests/<Проект>` — если 0, проект
этот сервис **не** покрывает, как бы он ни назывался.

## Правила
- Писать можно **только в эту папку** (`9-tests`). Правки в `6-msa` (бэкенд) и `8-front` (фронт) —
  только после согласования с TechLead через `5-tech-backlog/chat/te.md` (`→ @TL`).
- Тесты ссылаются на сервисы из `6-msa` через `ProjectReference` (относительные пути).
- Запуск: `dotnet test` на соответствующем `.csproj`/`.sln`.

## Границы решений (владелец 2026-11-16, через @LM `chat/lm.md`)

- **Процесс — только @LM:** правила, линтеры, `loop.config.json`, `AGENTS.md`, инструменты
  `tools/**`. Затык с инструментом → идти к @LM; **не править самому**.
- **Архитектура — только @AR** и только после обсуждения с владельцем: ADR, канвасы,
  границы сервисов, `04-architecture/**`.

**Что это значит для `9-tests` практически:**

| Отношение | Пути |
|---|---|
**моё** | `9-tests/RnD/front-e2e/**` (спеки, хелперы), `9-tests/scripts/**` (приёмочные скрипты), `5-tech-backlog/tester/**`, `chat/te.md` |
**не моё** | `tools/check_*.py` (процессные гейты — @DO/@LM), `.github/workflows/**` (@DO), `loop.config.json` / `AGENTS.md` (@LM), `04-architecture/**` (@AR) |
**новый гейт** | пишу скрипт в `9-tests/scripts/`, доказываю норму **и** дефект, **подключение в CI прошу @DO**, изменение процессных правил — **через @LM** |
**расхождение с нормой** | описываю строкой `→ @AR`/`→ @TL`, а не правлю чужой документ |

Факт-проверка (на `2026-11-04`, правило действует с этой даты):
`git log --oneline -12 gh/main -- loop.config.json AGENTS.md tools` — ни одного `te:`-коммита;
последние правки `tools/check_front_e2e.py` — `do(TE-035)` (@DO), то есть инварианты
`global-setup` вносил он, а не я.

## Структура (ADR-028 ред. 3 — тесты живут в папках сервисов)

Обновлено `2026-11-04` после миграции: **в `9-tests` больше нет сервисных тестов**, здесь
остались только фронтовые (браузер) и приёмочные скрипты. Тест сервиса — в его папке
(`6-msa/BC-<X>/src/*Tests`), тест пары — у потребителя (`<Потребитель>.ApiTests`).

```
9-tests/
  RnD/front-e2e/          — фронтовые Playwright-спеки (маркер `front-e2e.md`, зона @TE)
  scripts/                — приёмочные скрипты (ADR-028, 0.88, ADR-028-изоляция) + Python-двойники
```
Нагрузка переехала: `7-deploy/load/{load.js,load.ps1,README.md}` (зона @DO).

## Карта TE → тесты (актуальные проекты, проверено `git ls-tree` на `2026-11-04`)

| TE | Проект (путь в `origin`) | Тип |
|---|---|---|
| TE-001 | `6-msa/BC-Journal/src/Journal.IntegrationTests` (+ `Journal.Domain.Tests`, `Journal.ApiTests`) | интеграционные (домен) |
| TE-002 | `6-msa/BC-Partners/src/Partners.IntegrationTests`, `6-msa/BC-Catalog/src/Catalog.IntegrationTests` | интеграционные |
| TE-003 | `6-msa/BC-SomaticSeries/src/SomaticSeries.IntegrationTests`, `6-msa/BC-DailyMetrics/src/DailyMetrics.IntegrationTests` | интеграционные |
| TE-004 | `6-msa/ApiGateway.Tests/Gateways.IntegrationTests` (+ `…/GatewaysApi.IntegrationTests`) | интеграционные |
| TE-005 | `6-msa/BC-Analytics/src/Analytics.ApiTests/AnalyticsPrivacy.IntegrationTests`, `6-msa/BC-Privacy/src/Privacy.IntegrationTests` | интеграционные |
| TE-006 | `6-msa/BC-Portability/src/Portability.ApiTests/PortabilityGateway.IntegrationTests` | интеграционные |
| TE-007…009 | `6-msa/BC-Client/src/Client.ApiTests/ApiTests` (HTTP, env-gated `XDIARY_E2E_BASE_URL`) | E2E |
| TE-010…011 | `7-deploy/load/` (k6 `load.js` + `load.ps1` + план) | нагрузка |
| TE-050 | `6-msa/BC-Journal/src/Journal.ApiTests/PartnerSoftDeleteTests.cs` (5 `[ApiFact]`) | контракт (мягкое удаление) |
| — | пары (ADR-028): `BC-Client/…/ContractClientJournal.IntegrationTests`, `BC-Catalog/…/PartnersCatalog.IntegrationTests`, `BC-DailyMetrics/…/SomaticDaily.IntegrationTests` | consumer-driven |
| — | фронт: `9-tests/RnD/front-e2e/**` | Playwright (браузер) |

Всего тестовых проектов в `origin`: **31** (сверка — `git ls-tree -r --name-only origin/main | grep -c 'Tests.*csproj'`).

## Пределы песочницы агента (важно для прогонов)

- **`dotnet test` локально не запускается:** `testhost` падает на старте с
  `System.ComponentModel.Win32Exception (5)` в `Process.GetProcessHandle` — песочница
  запрещает работу с handle'ами процессов. Это **не** падение теста и **не** красный продукт;
  `dotnet build` при этом работает (пакеты в кэше).
- **Стенды недоступны:** `xdiary-vps2…` → «Базовое соединение закрыто», Playwright не стартует.
- Вывод: карточки, у которых критерий — «прогон», закрываются **по квитанции CI/@DO**, а не
  по «у меня локально прошло». Число `passed` из песочницы получить нельзя — можно только
  сборку, структуру тестов и наличие маршрутов, которые тест вызывает.

## Статус по TE
- TE-001..TE-004 — ✅ закрыта (TL).
- TE-005 (analytics+privacy) — ✅ закрыта (TL 23:30); JWT-выдача реализована в **BE-014**.
- TE-006 (portability+gateway) — ✅ закрыта (TL 23:30); дедуп по `id` реализован в **BE-015**.
- TE-007…TE-009 (E2E) — тесты готовы (`E2E/E2E.Tests`, проект `E2E.IntegrationTests.csproj`), ждут стенд (`XDIARY_E2E_BASE_URL`); BE-014 готов.
- TE-010…TE-011 (нагрузка) — план+скрипты готовы (`Load/`), ждут стенд.
- CI: **DO-009** (`ci-tests.yml`, гоняет `9-tests/**/*.IntegrationTests.csproj`).
- Находка: шлюз роутит `/api/v1/somatic-series/**`, а контроллер сервиса — `/api/v1/somatic` → 404 (ждёт выравнивания).

## Сборка и CI
- CI (**DO-009**, `.github/workflows/ci-tests.yml`): **✅ зелёный** — run #16 (`e4cbc46`), шаг «Run integration tests (9-tests)» success (~65 с). Гоняет `dotnet test` по каждому `9-tests/**/*.IntegrationTests.csproj` на ubuntu-latest. E2E-проект `E2E.IntegrationTests.csproj` тоже в CI (тесты env-gated → skipped без стенда).
- Локально (Windows, это окружение) сборка 6-msa-зависимых тест-проектов падает **MSB4276** (битые workload-локаторы .NET SDK 8.0.419) и **NU1900** (нет сети) — это среда, не код. Авторитетный прогон — CI.
- E2E/нагрузка дополнительно требуют `XDIARY_E2E_BASE_URL` (стенд) и `XDIARY_E2E_TOKEN`/`XDIARY_E2E_PIN`.

## Квитанция (ii-b): только ссылка + как пользоваться инструментом

> **`ADR-031` — `Отклонено (2026-11-05)`** (предложение снято автором: копия канона создала бы
> второй источник правды; текст оставлен как история). Поэтому норма — `ADR-026`, а не `ADR-031`.
> Статусы проверены по `origin`: `adr-026.md` — 961 строка, `Пересмотрен (2026-11-05)`; `adr-031.md` — `Отклонено`;
> коммиты-основания: `77ea61ad` (статусы 007/025/026), `0bd4c9fe` (канон в `ADR-026`, `ADR-031` — rejected).

### Схемы `Events`/`Aggregate`/`MaterializedViews` — контракт (ADR-034 `Принято`)

`ADR-034` принят владельцем (`2026-11-05`, `adr-034.md` — 227 строк, статус `Принято`; кворум 3 из 5).
Следствие для тестов, зафиксированное в §5 условиями п.1–2:

- **имя схемы — часть контракта**; переименование после принятия = **новый ADR** (иначе проверки
  останутся на `Commands` и будут «зелёными на пустоте»);
- **критерий готовности включает** «имя схемы в проверке обновлено на `Events`».

Пока §5 не выполнен (событие сохраняется -> `Handle` исполняется -> представление строится проекцией),
проверки смотрят на `Commands`, и это **допустимо**: раскладка не начата. Когда начнётся — имя схемы
передаётся аргументом (`accept_read_path --schema-present`, `accept_pipeline_stages`), а **старый**
аргумент обязан давать «не измерено», а не зелёное.

### Матрица трёх норм по приёмочным инструментам

Нормы: **провенанс** (`--measured-on` для поведенческого), **квитанция со сходимостью**
(`засчитано + не засчитано + нарушено = всего`), **`--selftest`** с красным контролем.
Проверено по `origin` `2026-11-05`: в `9-tests/scripts` **14** скриптов.

| Скрипт | Строк | Провенанс | Квитанция | `--selftest` |
|---|---|---|---|---|
| `accept_pipeline_stages.py` | 181 | да | да | 9/9 |
| `accept_read_path.py` | 425 | да | да | 29/29 |
| `check_front_readpath.py` | 268 | нет (env-флаги) | да | 9/9 |
| `check_apim_auth.py` | 1305 | н/п (живые коды) | да | 27/27 |
| `find_stamp.py`, `file_same_as.py`, `check_read_paths.py`, `check_front_088.py`, `check_front_specs_base.py`, `check_adr028_move.py` | 140–338 | н/п (статические) | н/п (один вердикт) | 19/6/10/4/5/10 |
| 5 shell-скриптов (`check-adr028-move.sh`, `check-front-088.sh`, `check-journal-scope.sh`, `check-profile-canon.sh` и пара к `check_adr028_move.py`) | 66–286 | — | — | **нет** |

**Открытый пробел (зафиксирован, не «забыли»):** у пяти shell-гейтов `--selftest` нет; при этом
`check_adr028_move.py` и `check_front_088.py` самопроверку имеют — то есть `.sh`-двойники дублируют
проверяемые скрипты. Решение: не заводить кейсы в shell, а оставить один из двух (зона @TE).

**Два гейта, два вопроса (не путать):**

| Гейт | Вопрос | `rc=2` значит |
|---|---|---|
`tools/check_eventstore_ready.py` (@DO, 150 строк, `--selftest` 5/5) | **можно ли начинать раскладку** (§5: три условия) | «предусловия ещё не выполнены» — **нормальное** состояние до раскладки |
`9-tests/scripts/accept_pipeline_stages.py` (@TE, 181 строка, `--selftest` 9/9) | **работает ли конвейер сейчас** (`subscribed` → `migrations` → `[handle]` → `read`) | «не хватает данных замера» (например, нет `--measured-on`) |

Ни в одном случае `rc=2` не означает «плохо» и не означает «зелено».

**Общая семантика `rc` для приёмочных гейтов:** `0` — измерено и пройдено, `1` — измерено и нарушено,
`2` — **не измерено** (нет данных / нет провенанса / таблицы ещё нет). Тот же смысл у `check_readstore_live.py`
(`@DO`): `--schema Events` до раскладки даёт `rc=2`, а не `rc=1`, — иначе CI краснел бы за несделанную работу.

### Приёмка конвейера по ступеням

`9-tests/scripts/accept_pipeline_stages.py` — четыре ступени в фиксированном порядке
(`subscribed` → `migrations` → `[handle]` → `partners_read`), каждая отдельным вердиктом;
четвёртая **не засчитывается**, пока предыдущая провалена. Требует `--measured-on` для
поведенческих ступеней; `rc=0` — все пройдены, `rc=1` — есть нарушение, `rc=2` — **не хватает данных**
(это не «плохо»). `--selftest` 8 кейсов + кейс на текст (сходимость квитанции, отсутствие `None`).

**Норма — только `ADR-026 → «Квитанция
`ITERATION-PLAN §19.1` ссылается на него. Здесь намеренно **нет** пересказа нормы: два описания
расходятся на первой правке (класс уже вычищался трижды: `2xx`-only → `400` в нарушители → `contract`).

Реализация: `9-tests/scripts/check_apim_auth.py` — канон замера (решение @TL). `tools/check_service_auth.py`
(@DO) — перекрёстный контроль, его числа печатаются с пометкой «cross-check, не канон» и обязаны
совпасть после разложения по классам. Доказательства приёмки — `9-tests/reports/ii-b-gate-verification.md`.

> **Где искать канон после правок ADR (соглашения с владельцем, 2026-11-05, `chat/ar.md` `[21:00]`):**
> принятые ADR **не переписываются** — меняется только статус (`Пересмотрен (дата)` / `Заменён (дата)`),
> а уточнения идут **новым** ADR. Поэтому ссылка выше со временем может сменить адрес: текст канона
> переносится, например, в `ADR-026` — **при этом не меняются ни команды, ни поля, ни значения**
> квитанции, меняется только место ссылки (о переносе @AR предупредит строкой).
> Сейчас `ADR-026` — `Принято`, а `ADR-030` (APIM как единственный шлюз) — **`Предложено`**: по
> соглашению «до вердикта владельца ADR не порождает работ» работы по нему не начинаются.

**Запуск:**
```bash
python 9-tests/scripts/check_apim_auth.py --base https://<вход>                 # ii-a: публичный вход (YARP)
python 9-tests/scripts/check_apim_auth.py --direct --network xdiary \
       --images auto --where "podman run --network xdiary"                        # ii-b: изнутри контура
python 9-tests/scripts/check_apim_auth.py --selftest                            # без сети: 19/19
python 9-tests/scripts/check_apim_auth.py --dry-run --direct                    # план: BC → сервис, 12/76
python 9-tests/scripts/check_apim_auth.py --direct --only Catalog,Analytics     # срез (нужен --network)
```

**Флаги, которые нужны на практике:**
- `--base <url>` — мерить через вход (прокси); `--direct` — бить в сервисы напрямую (обязателен `--network`);
- `--network <имя>` — сеть контура; без него `--direct` возвращает `rc=2` (недействительно, а не «зелено»);
- `--where "…"` — чем измерено; без него результат не засчитывается (`rc=2`, «нет provenance»);
- `--images auto` — digest'ы контейнеров через `podman inspect`; если podman нет — `НЕ ИЗМЕРЕНО` (не `REF`);
- `--service-map map.json` — свои адреса (`host:port` | URL | имя сервиса); `--only BC,…` — срез;
- `--5xx-unknown` — вернуть `5xx` в `unknown` (по умолчанию в `--direct` это нарушитель).
- `--strict-via` — требовать `via=gravitee` для **всех** условий (по умолчанию строго только `(ii-a)`);
- `--probe-entry` — доказать, **что стоит в пути**: заголовки (`X-Gravitee-*`) + поведение
  несуществующего пути (`404` у шлюза против `401` у приложения). Квитанция всегда печатает
  `via=` — **кто ответил** (`gravitee` | `yarp` | `gateway-no-route` | `НЕ ОПРЕДЕЛЕНО`);
  `(ii-a)` засчитывается только при `via=gravitee`, иначе `rc=2` («не засчитано» — лечится путь,
  а не методика). `(ii-c)` проверяется **на том входе, который есть**: `401`/`404` на YARP —
  честный ответ «внутренняя ручка недостижима». Флаг `--strict-via` распространяет требование
  `via=gravitee` на все условия.

**Пример вывода** (последняя строка, её и копируют):
```
REF=9257a1d2 | (ii-a) UNKNOWN защищено=0/76 (не засчитывается) | нарушителей=0 | contract=0 |
неизвестно=76 | rc=2 | сумма=76/76 | where=http://127.0.0.1:19999 | images=НЕ УКАЗАНО |
network=НЕ УКАЗАНО | tool=9-tests/scripts/check_apim_auth.py@e77f5c571b8b
```

**Коды возврата:** `0` — всё измерено и чисто; `1` — есть нарушители или `contract`;
`2` — результат недействителен или не измерен (ни одного ответа, не сходится сумма по целям, база не
резолвится, `--direct` без `--where`/`--network`).

## Как я коммичу: правка «применилась» ≠ правка «в файле»

Две проверки, обе из реальных сбоев 2026-11-05 (класс «команда сказала ок ≠ сделано»):

1. **`py_compile` / `--selftest` доказывают синтаксис и логику, но не сохранение файла.**
   Скрипт может напечатать успех, а содержимое остаться прежним (here-string с бэктиками;
   правка, откатившаяся при параллельной записи). Проверка **перед коммитом**:
   ```bash
   git status --porcelain -- <путь>     # пусто ⇒ правки НЕТ, что бы ни говорил py_compile
   ```
2. **Проверять результат из `origin`, а не по сообщению скрипта** (@AR `chat/ar.md` `[2026-11-05 19:30]`):
   ```bash
   git show origin/main:<путь> | grep -F "<ожидаемая строка>"
   ```
   Только это отвечает на вопрос «дошло ли», потому что рабочее дерево — не доказательство.

**Почему проверять нужно именно перед коммитом:** в моём случае оба патча применились и `git status`
показывал изменения, но к моменту коммита файл уже совпадал с `origin` — то есть проверка
«сразу после правки» этот сбой не поймала бы. Ловит только проверка **в момент коммита**:
`git diff --cached --stat` обязан содержать каждый путь, который я называю изменённым.

## Остальные приёмочные гейты (мои, в `9-tests/scripts/`)

Все — без сети и браузера, у каждого `--selftest` с красным контролем на **каждом** критерии
(правило: новый критерий = новый кейс в том же коммите). В CI (`.github/workflows/ci-guards.yml`)
подключены `check_front_readpath` (строки 193–194) и `check_front_specs_base` (203–204), причём **у обоих** `--selftest` идёт строкой выше нормы (проверено по файлу, `2026-11-05`);
`check_adr028_move` — **не подключён** (вспомогательный для ревью, не гейт), `check_read_paths` и
`accept_read_path` — подключение за @DO.

```bash
python 9-tests/scripts/check_read_paths.py --selftest      # 9/9
python 9-tests/scripts/check_read_paths.py                 # A/B/C + полнота «N из M»
python 9-tests/scripts/check_front_readpath.py --selftest  # 9/9
python 9-tests/scripts/check_front_readpath.py             # read-path + stale-note + busyRun + версия
python 9-tests/scripts/check_front_088.py                  # версия фронта (PASS на 0.94)
python 9-tests/scripts/accept_read_path.py --selftest      # 23/23
python 9-tests/scripts/file_same_as.py --selftest          # 5/5
```

**`file_same_as.py`** — «этот файл тот же, что в `ref`?» отвечает **хэшем**, а не пересчётом строк
(`git hash-object` против `git rev-parse <ref>:<path>`). Правило дня от @AR (`chat/ar.md`
`[2026-11-06 00:50]`): `Measure-Object -Line` и `splitlines()` дают «около» — на них уже дважды
делались неверные выводы. `SAME` / `DIFF` / `UNKNOWN`; `rc=0/1/2` (2 — ref или путь не читается).

```bash
python 9-tests/scripts/file_same_as.py --ref origin/main -- 9-tests/README.md README.md
```

**`check_read_paths.py`** — сверка `read-paths.md` §2 ↔ `api-index.yaml` в **обе** стороны плюс
покрытие: **A** §2 → индекс; **B** MV, читаемый фронтом, → §2; **C** каждый `Api.*.*()` из §2 обязан
иметь строку в `SOURCES` (иначе `rc=1`). Неполнота реестра — **`INFO`** с числом
(«проверено 6 из 18»), а не красное: принцип @AR «гейт блокирует только то, что автор правит в том же
коммите». `rc=0/1/2` (2 — вход не читается).

**`check_front_readpath.py`** — 5 предикатов по `8-front`: артефакты read-path; `role="alert"`;
наблюдаемость (`toast` + host `#view-wizard`); **порядок** `reportReadFailure` после `renderWizard`;
**`stale-note`** (`FE-044`: бейдж, `role="status"`, ранний выход по `fromCache`, ≥3 вьюхи, синхрон
порогов `24/24`); `busyRun` на трендах и CSV; синхрон `APP_VERSION`↔`CACHE`. `rc=0/1/2`.

**`accept_read_path.py`** — квитанция приёмки read-моделей (`TE-050`/`BE-068`/`BE-071`). Принимает
**числа и содержание** и различает три состояния: `0` — всё измерено и в норме; `1` — нарушение;
`2` — **не засчитано** (нет данных). Ключевые флаги:
- `--source-kind historical|live` — для `live` (E1: командная таблица больше не пишется) числовая
  сверка **не применяется**, доказательство — `--projection-verified-by`;
- `--journal-empty-partner-names[-src]` — различение `both_empty` (норма) и `lost_names` (`rc=1`);
- `--partners-name-mismatches`, `--contraceptive-count-mismatches` — содержание, не счётчик;
- `--event-count`/`--rerun-event-count` — идемпотентность **по событиям**;
- `--write-path projection|sql` + `--repair-declared-in`/`--repair-date`/`--repair-command`:
  `sql` без полного объявления → `rc=2`, а `.sql`-путь обязан **существовать** (`--repo-root`).

```bash
python 9-tests/scripts/accept_read_path.py \
  --partners-read 13 --partners-src 13 --journal-read 4 --journal-src 4 \
  --partners-http 200 --journal-http 200 --contract-passed 4 --contract-failed 0 \
  --partners-name-mismatches 0 --journal-empty-partner-names 4 \
  --journal-empty-partner-names-src 4 --contraceptive-count-mismatches 0 \
  --event-count 4 --rerun-event-count 4 --projection-verified-by partners-read-parity
```


## Runbook: E2E и нагрузка на стенде (TE-007..011)
1. Получить access-токен: `POST {BASE}/api/v1/privacy/token` с телом `{"pin":"<PIN>"}` → `{accessToken, refreshToken}`.
2. **E2E** (TE-007..009):
   ```bash
   # одной командой (скрипт):
   BASE="https://xdiary-vps1.taile0b6ee.ts.net" PIN="1234" ./9-tests/E2E/run-e2e.sh
   # или напрямую:
   XDIARY_E2E_BASE_URL="https://xdiary-vps1.taile0b6ee.ts.net" XDIARY_E2E_PIN="1234" \
     dotnet test 9-tests/E2E/E2E.Tests/E2E.IntegrationTests.csproj
   ```
   Без `XDIARY_E2E_BASE_URL` тесты пропускаются (skipped), не падают.
3. **Нагрузка** (TE-010..011, задачи **отклонены PO 31.10**: нагрузочное тестирование не нужно) — скрипты переезжают в `7-deploy/load/` (перенос @DO, ADR-028 ред. 3: в `9-tests` только браузерные тесты). До переноса файлы лежат в `9-tests/Load/`: `k6 run -e XDIARY_E2E_BASE_URL=... -e XDIARY_E2E_TOKEN=... <путь>/load.js`
4. Метрики стенда: `podman stats` по контейнерам + `pg_stat_activity`; отчёт — по шаблону `Load/README.md`.
5. **Известный блокер TE-009:** шлюз роутит `/api/v1/somatic-series/**`, а контроллер — `/api/v1/somatic` → 404. Пока не выровнено, E2E-срез соматики будет падать.

### Вариант B (решение TL 2026-10-31 03:00) — прогон на VPS-1
Всё выполняется **на VPS-1**, где стек доступен на `http://localhost:8080`:
```bash
# 1) DevOps ставит .NET 8 SDK:  apt-get install -y dotnet-sdk-8.0
# 2) E2E — одной командой (сам проверит health + dotnet):
./9-tests/E2E/run-e2e-vps.sh
#    PIN при необходимости: XDIARY_E2E_PIN=1234 ./9-tests/E2E/run-e2e-vps.sh
# 3) Нагрузка (TE-010..011) — см. вывод скрипта: получить TOKEN и запустить k6.
```
Скрипт `run-e2e-vps.sh` сам: проверяет `/health`, проверяет `dotnet`, гоняет `E2E.IntegrationTests.csproj`, и печатает точные команды для нагрузки.

## Методология измерения флейкости (E2E)

Автор формулировки — @LM; зафиксировал @TE (зона `9-tests/`). Обновлено: 2026-11-02.

1. **`5/5` зелёных ≠ «флейкости нет».** Это **верхняя граница**: «пять повторов её не поймали».
   Писать в задаче «флейкости нет» на основании 5/5 нельзя — только «на 5 повторах не
   воспроизвелась». Обратное (`1/5` красный) — доказательство флейкости.

2. **Изолированный и полный прогон — РАЗНЫЕ условия.** Пример: `csv` изолированно дал
   5/5, а в полном прогоне упал (timeout на `#btn-import-open`). Значит часть «флейкости» —
   это **зависимость от состава/порядка прогона и накопленного состояния клиента**
   (IndexedDB одного origin: записи, соматика, настройки, модальные слои). Поэтому
   результат одиночного `--repeat-each` **не переносится** на полный прогон и наоборот.

3. **`--workers=1` обязателен** при измерении флейкости. Иначе несколько воркеров делят
   один dev-стенд и **создают** гонку, которую мы пытаемся измерить. Замер с воркерами > 1
   измеряет не продукт, а конкуренцию за стенд.

4. **Сетевое падение — не доказательство красного продукта.** Пример: единственное
   падение `settings` (4/5) — `page.evaluate: TypeError: Failed to fetch`, то есть сеть/API.
   Возможен общий источник с `somatic-series/slice` (`net::ERR_FAILED`) — **гипотеза**, кодом
   фронта пока не подтверждена (в `8-front/js` нет `setInterval`-поллинга). Для вывода нужен
   стек из аннотации, а не совпадение текста ошибки.

5. **Одинаковая ошибка в двух спеках = искать общий источник.** Если `page.evaluate:
   TypeError: Failed to fetch` встречается и в `settings`, и в `somatic-series/slice`
   (`net::ERR_FAILED`), это скорее один общий helper/сеть, чем два независимых дефекта.
   Правят **источник**, а не каждый спек по отдельности.

**Важно про browser-context:** Playwright даёт **новый context на каждый тест**, поэтому
IndexedDB/localStorage **не переносятся** между спеками. «Утечка состояния браузера» как
объяснение флейкости **не работает**; накопление идёт **на стенде** (одна dev-БД для всех
прогонов), а не в браузере. Проверка гипотезы — прогон с явной последовательностью:
`npx playwright test settings.spec.js csv.spec.js --workers=1`.

6. **«Пустой» прогон — не доказательство (D-12).** Если в прогоне не случилось **измеряемого
   события** (например, ни одного тоста, ни одной ошибки), он не подтверждает ни «до», ни
   «после»: это не «лишний» прогон, а прогон **без сигнала**, и он опаснее избыточного, потому
   что выглядит как доказательство. Пример: прогон `#13` с `repeat=1` не дал ни одного тоста,
   и только `#14` дал пару «до/после». **Порядок:** перед `workflow_dispatch` проверить, нет ли
   уже прогона на нужном `main`-коммите — `ci-front` на том же коммите закрывает
   диагностическую правку без нового запуска (@DO 03:00, @LM 03:21).

**Практическое правило:** перед выводом «стабильно/флейко» фиксировать (а) число повторов,
(б) `--workers`, (в) полный или изолированный прогон, (г) были ли сетевые ошибки,
(д) случилось ли в прогоне вообще **измеряемое событие** (иначе вывод не про что).

## PDR-19: максимальное покрытие шлюза и клиента (A + C, решение владельца)

Обоснование: оба места уже дали инциденты — **502 через шлюз** и **неразвёрнутый `/clients`**.
Тестов на маршрутизацию `ApiGateway` **не было нигде** (`9-tests/Gateways` — это Garmin и
AppleHealth, см. раздел «Имя проекта ≠ имя сервиса»).

### 1. Критерий: что обязано быть покрыто у `ApiGateway`

| Поведение | Ожидание |
|---|---|
| `GET /health` | 200, без авторизации |
| `GET /api/v1/clients/me` без токена | **401** + `WWW-Authenticate: Bearer` |
| `GET /api/v1/clients/me` с токеном | 200, проксирует ответ сервиса; **после E7/ADR-024** `linkedPartnerId` в ответе **отсутствует** (проверено 2026-11-11: поля `id, phone, displayName, gender, photoUrl, verified`) |
| `POST /api/v1/clients` | 200/201, тело доходит до сервиса |
| `POST /api/v1/clients/refresh` | **анонимен** — 401 не возвращает |
| неизвестный путь (`/api/v1/does-not-exist`) | **401** (не 502/504) — фактическое поведение с 2026-11-11; клиент не отличает «маршрута нет» от «не авторизован», находка зафиксирована в `Gateways.Api` (B6) |
| апстрим недоступен | **502** (соединение отклонено) или **504** (таймаут) |
| `Authorization` | прокидывается **как есть** в апстрим (проверять на стороне сервиса) |

### 2. Контракт «шлюз → сервис»: что реально дёргает фронт

Собрано `git grep` по `8-front/js` (не по названиям, а по факту вызовов):

**Клиент (BC-Client):**
```
POST /api/v1/clients                    создание
POST /api/v1/clients/{id}/verify        OTP → client-JWT
POST /api/v1/clients/{id}/link-partner  шим ADR-024: 200 + `Deprecation: true`, удаляется после 0.88
GET  /api/v1/clients/me                 verified (+ профиль `displayName`/`gender`); `linkedPartnerId` **больше нет** (E7)
POST /api/v1/clients/refresh            обмен refresh → новая пара
GET  /api/v1/clients                    **маршрут удалён**: анонимно 401, с токеном 405 (ADR-026 §3)
**Журнал (BC-Journal):**
```
POST /api/v1/journal/records
GET  /api/v1/journal/recent
GET  /api/v1/journal/places
GET  /api/v1/journal/fantasies
```
**Партнёры/каталог:** `POST|GET /api/v1/partners`, `GET /api/v1/catalog`.

**Главный контрактный тест** (актуальная форма, `Point1` + `Point2b` в `Contract.ClientJournal`): `POST /clients` → `/verify` → **`POST /clients/me/profile`** (`displayName` + `gender` по канону `male`/`female`) → **`GET /clients/me`**, где `verified === true`, `gender` — канонический, а `linkedPartnerId` **отсутствует**.
Историческая форма (до E7) шла через `POST /partners` + `link-partner` — оставлена только как шим (`Deprecation: true`) и **не** является цепочкой онбординга.
### 3. Где это живёт

- маршруты шлюза → **новый** `9-tests/Gateways.Api` (создаёт @BE по задаче от @TL);
- контракт клиента/журнала → `9-tests/Contract.ClientJournal` (@TE);
- карта в этом README обновляется **тем же коммитом**, что добавляет проект —
  иначе краснеет гейт `check_test_map.py`.
