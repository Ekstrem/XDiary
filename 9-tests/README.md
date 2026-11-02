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

## Структура
```
9-tests/
  Journal/Journal.IntegrationTests/              — TE-001 (журнал: Command → Query → Assert + инварианты)
  Partners.Catalog/PartnersCatalog.IntegrationTests/ — TE-002
  Somatic.Daily/SomaticDaily.IntegrationTests/   — TE-003
  Gateways/Gateways.IntegrationTests/            — TE-004
  Analytics.Privacy/AnalyticsPrivacy.IntegrationTests/ — TE-005
  Portability.Gateway/PortabilityGateway.IntegrationTests/ — TE-006
  E2E/E2E.Tests/                                 — TE-007..TE-009 (HTTP, env-gated по XDIARY_E2E_BASE_URL)
  Load/                                          — TE-010..TE-011 (k6 load.js + load.ps1 + план)
```

## Карта TE → тесты
| TE | Файлы | Тип |
|---|---|---|
| TE-001 | `Journal/Journal.IntegrationTests/{RecordLifecycleTests,RecordInvariantTests}.cs` | интеграционные (домен) |
| TE-002 | `Partners.Catalog/PartnersCatalog.IntegrationTests/{PartnerTests,CatalogTests}.cs` | интеграционные |
| TE-003 | `Somatic.Daily/SomaticDaily.IntegrationTests/{SomaticSeriesTests,DailyMetricsTests}.cs` | интеграционные |
| TE-004 | `Gateways/Gateways.IntegrationTests/{GarminGatewayTests,AppleHealthGatewayTests}.cs` | интеграционные |
| TE-005 | `Analytics.Privacy/AnalyticsPrivacy.IntegrationTests/{AnalyticsTests,PrivacyTests}.cs` | интеграционные |
| TE-006 | `Portability.Gateway/PortabilityGateway.IntegrationTests/{PortabilityTests,GatewayJwtTests}.cs` | интеграционные |
| TE-007…009 | `E2E/E2E.Tests/{AuthE2ETests,JournalE2ETests,SomaticAnalyticsE2ETests}.cs` | E2E (HTTP, env-gated) |
| TE-010…011 | `Load/{load.js,load.ps1}` + `Load/README.md` | нагрузка (скрипты + план) |

## Статус по TE
- TE-001..TE-004 — ✅ закрыта (TL).
- TE-005 (analytics+privacy) — ✅ закрыта (TL 23:30); JWT-выдача реализована в **BE-014**.
- TE-006 (portability+gateway) — ✅ закрыта (TL 23:30); дедуп по `id` реализован в **BE-015**.
- TE-007…TE-009 (E2E) — тесты готовы (`E2E/E2E.Tests`, проект `E2E.IntegrationTests.csproj`), ждут стенд (`XDIARY_E2E_BASE_URL`); BE-014 готов.
- TE-010…TE-011 (нагрузка) — план+скрипты готовы (`Load/`), ждут стенд.
- CI: **DO-009** (`ci-tests.yml`, гоняет `9-tests/**/*.IntegrationTests.csproj`).
- Находка: шлюз роутит `/api/v1/somatic-series/**`, а контроллер сервиса — `/api/v1/somatic` → 404 (ждёт выравнивания).

## Сборка и CI
- Solution: **`9-tests/XDiary.Tests.sln`** (7 проектов).
- CI (**DO-009**, `.github/workflows/ci-tests.yml`): **✅ зелёный** — run #16 (`e4cbc46`), шаг «Run integration tests (9-tests)» success (~65 с). Гоняет `dotnet test` по каждому `9-tests/**/*.IntegrationTests.csproj` на ubuntu-latest. E2E-проект `E2E.IntegrationTests.csproj` тоже в CI (тесты env-gated → skipped без стенда).
- Локально (Windows, это окружение) сборка 6-msa-зависимых тест-проектов падает **MSB4276** (битые workload-локаторы .NET SDK 8.0.419) и **NU1900** (нет сети) — это среда, не код. Авторитетный прогон — CI.
- E2E/нагрузка дополнительно требуют `XDIARY_E2E_BASE_URL` (стенд) и `XDIARY_E2E_TOKEN`/`XDIARY_E2E_PIN`.

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
3. **Нагрузка** (TE-010..011): `k6 run -e XDIARY_E2E_BASE_URL=... -e XDIARY_E2E_TOKEN=... 9-tests/Load/load.js`
   или PowerShell: `./9-tests/Load/load.ps1 -Minutes 10 -Out report.csv`.
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

**Практическое правило:** перед выводом «стабильно/флейко» фиксировать (а) число повторов,
(б) `--workers`, (в) полный или изолированный прогон, (г) были ли сетевые ошибки.