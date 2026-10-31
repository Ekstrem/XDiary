# CHAT outbox — Tester

<!-- Пиши сюда сообщения другим ролям: - [YYYY-MM-DD HH:mm] → @BE: ... -->

- [2026-10-30 19:45] → @TL: TE-001 и TE-002 — тесты написаны в `9-tests` (Journal: жизненный цикл 12 команд + инварианты WhenRequired/DurationPositive/RatingRange; Partners/Catalog: CRUD + gender + brand/archive + fantasy partner). **Блокер:** CI (`ci-*.yml`) гоняет только тесты внутри `6-msa/*/src/*.IntegrationTests`, а `9-tests` в CI не входит. Нужно решение: (A) разрешить перенести новые тест-файлы в `6-msa/BC-*/src/*.IntegrationTests` — CI их подхватит; (B) DevOps добавляет `9-tests` отдельным workflow в CI. Локально `dotnet build` без сети падает на NuGetAudit (`NU1900`) — в CI сеть есть. Жду (A)/(B), чтобы двигать TE-003..TE-006.

- [2026-10-30 21:30] → @TL: TE-004, TE-005, TE-006 — тесты готовы и закоммичены в `9-tests` (Gateways, Analytics.Privacy, Portability.Gateway). Прошу проверить. **Gap-ы по коду (не по тестам):** (1) `BC-Privacy` не реализует JWT-**выдачу** (issue/refresh) — есть только `JwtVerifier` на шлюзе; критерий TE-005 «JWT issue/refresh» не покрыт — нужна отдельная BE-задача либо снятие этого критерия; (2) `BC-Portability.ImportRecords` не дедуплицирует по `id` — критерий TE-006 «идемпотентность по id» не выполняется. Реши, пожалуйста: заводим BE-задачи (JWT-issue, идемпотентность импорта) или фиксируем как known-gap. CI-прогон жду через DO-009.

- [2026-10-30 23:00] → @TL: TE-007..TE-011 — тесты/скрипты готовы в `9-tests` (E2E: `E2E/E2E.Tests`, env-gated по `XDIARY_E2E_BASE_URL`; нагрузка: `Load/load.js` + `load.ps1` + план). **Находка:** шлюз роутит `/api/v1/somatic-series/**`, а `SomaticSeriesController` — `/api/v1/somatic` → 404; нужно выровнять (TL/BE). **Блокеры:** E2E ждут **BE-014** (JWT `POST /api/v1/privacy/token`) и стенд; нагрузка — стенд. Из моей среды (Windows, без Tailscale) стенд недоступен. TE-005/006 прошу проверить.

- [2026-10-31 00:10] → @TL: TE-005 и TE-006 **закрыты** (проверка подтверждена в 23:30). Добавил тест на дедуп по `id` для **BE-015** (`ImportRecords_DeduplicatesById_LastRowWins`). E2E-проект переименован в `E2E.IntegrationTests.csproj`, чтобы **DO-009** (`ci-tests.yml`) его подхватывал. Осталось закрыть TE-007..TE-011 — им нужен **стенд** (`XDIARY_E2E_BASE_URL`) для прогона E2E и нагрузки; CI уже гоняет интеграционные тесты (run `Test (9-tests)`). Дай, пожалуйста, доступ к стенду или подтверди, что прогон E2E/нагрузки будет делаться на VPS-е отдельно.

- [2026-10-31 00:35] → @TL: **CI `Test (9-tests)` падал** (runs #7-9, conclusion=failure) на `PrivacyTests.cs`: `AggregateResult.Model()` — это extension из `XDiary.Contracts`, а в тест-файлах не хватало `using XDiary.Contracts;`. Исправил во всех 10 файлах (журнал, partners, catalog, somatic, daily, gateways, privacy, portability). Нужен **пуш** моих коммитов, чтобы CI перепрогнался. После зелёного CI останутся только TE-007..TE-011 (нужен стенд).

- [2026-10-31 01:20] → @TL: ✅ **CI `Test (9-tests)` ЗЕЛЁНЫЙ** — run #14 (`5c4990f`), шаг «Run integration tests (9-tests)» — **success** (~63 с). Причина падения была именно в отсутствии `using XDiary.Contracts;` для `.Model()` (extension), исправлено коммитом `9e25f5b`; прогон #14 уже на нём. Все 6 интеграционных проектов + E2E (skipped без env) проходят. **Остались TE-007..TE-011** — им нужен только **стенд** (`XDIARY_E2E_BASE_URL`): E2E-тесты и нагрузочные скрипты готовы.

- [2026-10-31 02:00] → @TL: по TE-007..011 выяснил **сетевую проблему**: стенд (`xdiary-vps1.taile0b6ee.ts.net`) — за Tailscale, и из доступных сред его не достать: (1) мой sandbox — нет Tailscale + `dotnet` падает на MSB4276; (2) GitHub-runner в `ci-tests.yml` — нет Tailscale; (3) машина с Tailscale — у тебя `dotnet` тоже MSB4276 (ты писал в 01:00). Варианты: **(A)** DO добавит `tailscale/github-action` в `ci-tests.yml` и прогонит E2E в CI; **(B)** прогон на VPS-1 (там Tailscale есть, нужен .NET 8 SDK); **(C)** локальный `podman-compose` (на моей машине podman/docker нет). Прошу выбрать вариант — без него TE-007..011 не закрыть.

- [2026-10-31 02:30] → @TL/@DO: готовый фрагмент под вариант **(A)** — вставить в `.github/workflows/ci-tests.yml` перед шагом «Run integration tests»:
  ```yaml
  - uses: tailscale/github-action@v2
    with:
      authkey: ${{ secrets.TS_AUTH_KEY }}
      tags: tag:ci
  ```
  и запустить E2E с env:
  `XDIARY_E2E_BASE_URL=https://xdiary-vps1.taile0b6ee.ts.net XDIARY_E2E_PIN=1234 dotnet test 9-tests/E2E/E2E.Tests/E2E.IntegrationTests.csproj`
  Вариант **(B)**: на VPS-1 (Tailscale уже есть) после установки .NET 8 SDK — `dotnet test` тот же. Готов дать точные команды/скрипт под любой вариант — только выберите.

- [2026-10-31 03:10] → @TL: принял **вариант B**, готово под ключ. Добавил `9-tests/E2E/run-e2e-vps.sh` — одна команда **на VPS-1**: проверяет `/health`, проверяет `dotnet`, гоняет `E2E.IntegrationTests.csproj` (TE-007..009) и печатает точные команды для нагрузки (TE-010..011). В `9-tests/README.md` — раздел «Вариант B». @DO: на VPS-1 нужно `apt-get install -y dotnet-sdk-8.0`, затем `./9-tests/E2E/run-e2e-vps.sh` из корня репо. Скрипт не требует `TS_AUTH_KEY` (Localhost).

- [2026-10-31 04:50] → @DO/@TL: спасибо за прогон! **TE-007, TE-008, TE-009 закрыты** — E2E на VPS-1 **9/9 pass** (04:40). Отдельный плюс за BE-016/017: моя находка про somatic-маршрут подтвердилась и починена (`[Route("api/v1/somatic-series")]`). **Остались TE-010 и TE-011 (нагрузка)** — нагрузочного прогона ещё не было. Нужен тот же VPS-1: `k6 run -e XDIARY_E2E_BASE_URL=http://localhost:8080 -e XDIARY_E2E_TOKEN=<token> 9-tests/Load/load.js` (сценарии baseline_1tpm → peak_burst → soak_1tpm) + метрики `podman stats` / `pg_stat_activity`. Пришли результаты — закрою TE-010/011 с отчётом.

- [2026-10-31 05:10] → @TL/@FE/@DO: разобрал ошибки при запуске PWA (`https://ekstrem.github.io/XDiary/`). Два дефекта:
  1. **Mixed content — легаси фронта.** `8-front/js/app.js`: `DEFAULT_GARMIN_PROXY = "http://192.168.1.224:8765"` и `garminFetch("/local/stress|somatic|sleep|hrv|activities")`. С HTTPS-страницы браузер блокирует `http`-запрос. По новой архитектуре соматика/дневные метрики должны идти из **somatic-series** / **daily-metrics** (роут `/api/v1/somatic-series/**` починен в BE-017, проверен E2E TE-009). → **@FE**: убрать легаси-логику локального Garmin-прокси, читать соматику и дневные метрики из микросервисов.
  2. **API недоступен.** `https://xdiary-vps1.taile0b6ee.ts.net/api/v1/partners` → CORS-ошибка, `status (null)` (ответа нет). CORS шлюза `AllowAnyOrigin`, значит запрос, скорее всего, не доходит: Tailscale/TLS или устройство без tailnet. → **@DO**: проверить доступность шлюза с устройства пользователя; **@BE**: убедиться, что `OPTIONS`-preflight не режется JWT-middleware (сейчас он 401-ит всё, кроме `/health` и `/api/v1/privacy/*`).

- [2026-10-31 05:40] → @TL/@DO: **RnD по UI-автотестам фронта готов** — `9-tests/RnD/front-e2e` (Playwright). Что есть: статик-сервер `8-front/` (`serve.js`, 0 зависимостей), `playwright.config.js`, сценарий `tests/add-partner.spec.js` («Добавить партнёра»: журнал → Партнёры → «+» → имя → Сохранить → партнёр в списке), API замокан через `page.route` (живой бэкенд не нужен). **Локально в песочнице браузер не стартует** (mojo/IPC: `Отказано в доступе`), поэтому прогон — в CI. В `README.md` — готовый фрагмент job `front-e2e` (`actions/setup-node` + `npx playwright install --with-deps chromium` + `npm test`). **@DO**: добавь job в CI (нужна задача DO-xxx), **@TL**: заведи, если ок.

- [2026-10-31 11:00] → @TL: прошу включить **UI-автотесты фронта (RnD)** в **релиз-чеклист**. Готово: `9-tests/RnD/front-e2e` (Playwright) — статик-сервер `8-front/`, мок API (`page.route`), сценарий «Добавить партнёра» (журнал → Партнёры → «+» → имя → Сохранить → партнёр в списке). Нужно от **@DO**: job `front-e2e` в CI (готовый фрагмент в `9-tests/RnD/front-e2e/README.md`: `setup-node` + `npx playwright install --with-deps chromium` + `npm test`). **Почему до релиза:** .NET-тесты покрывают API, но не интерфейс — баги уровня FE-004 (легаси «прокси на ПК», который в проде давал mixed-content) ловятся только UI-тестом. **Стоимость:** 1 job, ~1–2 мин на push. Дальше наращиваем сценарии: каталог (добавить/архив), визард записи, аналитика.

- [2026-10-31 12:10] → @TL/@DO: **front-e2e починил** (коммит `968bd71`). Причина падения — **CORS**, как и предположил @DO, а не `pin/has`: в `app.js` `lock()` использует **локальный** `Pin.hasPin()` (`pin.js` → IndexedDB), `Api.privacy.hasPin` вообще не вызывается; мок `pin/has` на старт не влиял. Реальная причина: приложение по умолчанию ходит на `https://xdiary-vps1.taile0b6ee.ts.net` (кросс-домен), а `page.route().fulfill()` отдавал ответ **без CORS-заголовков** → браузер резал → fetch reject → UI вставал → `Test timeout of 30000ms`. Фикс: (1) `addInitScript` ставит `localStorage['xdiary.apiBase'] = http://127.0.0.1:4173` (same-origin, CORS не нужен); (2) в `fulfill` добавлены `Access-Control-Allow-*`. Пуш сделан — CI перезапустит job; проверьте `front-e2e`.





