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

## Квитанция (ii-b): канон `ADR-026` → «Квитанция (ii-b)»

Единственный источник правды по полям — `ADR-026`; `ITERATION-PLAN §19.1` на него **ссылается**.
Реализация — `9-tests/scripts/check_apim_auth.py` (канон замера; `tools/check_service_auth.py` @DO —
перекрёстный контроль и обязан давать те же числа после разложения по классам).

**Формат строки (печатается последней, её копируют в карточку/ADR):**

```
REF=<sha> | (ii-a|ii-b) PASS|FAIL|UNKNOWN защищено=N/76 | нарушителей=V | contract=C | неизвестно=U |
rc=N | сумма=N+V+C+U/total | where=<чем измерено> | images=<svc:digest, …> | tool=<путь>@<sha256[:12]>
```

**Четыре класса:** `protected` = `401/403` · `violator` = `2xx/400/415/429` (+ `5xx` в `--direct`) ·
`contract` = `404/405` на операции, которая **есть** в `api-index.yaml` (дефект реестра, `DO-050`) ·
`unknown` = ответа нет (таймаут/соединение/`000`).

**Условия:** `PASS` — только при `V=0 И C=0 И U=0` (то есть `N = total`); тождество
`N+V+C+U = total` обязательно — иначе квитанция **недействительна** (`rc=2`, «выпавшая группа»).
`V` и `C` печатаются **раздельно** (`B = V + C` — производное; если печатать только его, сумму не проверить).
`tool=` называет версию **скрипта** (`sha256[:12]`), а не только репозитория: классификация менялась трижды за сутки,
и без этого «16» и «21» — числа из разных определений (ADR-026 §7).

**`5xx` по режимам (не двойной стандарт, а разные объекты):** `--base` — ответ **прокси** →
`[infra] unknown`; `--direct` — прокси в пути нет, ответ **сервиса** → **нарушитель**; вернуть в
`unknown` можно только явно: `--5xx-unknown`.

**Коды возврата:** `0` — измерено всё и нарушителей/contract/unknown нет; `1` — есть нарушители или
`contract`; `2` — недействительно/не измерено (нет ответов, сумма не сходится, база не резолвится,
`--direct` без `--where`).

**Запуск:**
```bash
python 9-tests/scripts/check_apim_auth.py --base https://<вход>            # ii-a: публичный вход (YARP)
python 9-tests/scripts/check_apim_auth.py --direct --images auto \
       --where "podman run --network xdiary"                               # ii-b: сервисы напрямую, изнутри
python 9-tests/scripts/check_apim_auth.py --selftest                      # без сети: 17/17
python 9-tests/scripts/check_apim_auth.py --dry-run --direct              # план: BC → сервис, 12/76
```
`--images auto` берёт digest из `podman inspect`; если podman нет — честно `НЕ ИЗМЕРЕНО` (а не `REF`).
`--only Catalog,Analytics` — срез; тождество тогда сверяется с числом целей, а не с литеральным `76`.


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
