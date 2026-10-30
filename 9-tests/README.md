# 9-tests — рабочая папка тестировщика

Здесь живут автотесты XDiary (интеграционные/E2E/нагрузочные), которые пишет роль **tester**.
Задачи — в `5-tech-backlog/tester` (TE-001…TE-011).

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

## Статус по TE
- TE-001 (journal) — ✅ закрыта (TL).
- TE-002 (partners+catalog) — ✅ закрыта.
- TE-003 (somatic+daily) — ✅ закрыта.
- TE-004 (gateways) — ✅ закрыта (TL, 21:40).
- TE-005 (analytics+privacy) — на проверке; **gap:** JWT-выдача в privacy не реализована → **BE-014**.
- TE-006 (portability+gateway) — на проверке; **gap:** идемпотентность импорта по `id` не реализована.
- TE-007…TE-009 (E2E) — тесты готовы (`9-tests/E2E`), ждут **BE-014** (JWT) + стенд.
- TE-010…TE-011 (нагрузка) — план+скрипты готовы (`9-tests/Load`), ждут стенд.
- CI: **DO-009**.
- Находка: шлюз роутит `/api/v1/somatic-series/**`, а контроллер сервиса — `/api/v1/somatic` → 404.

## Сборка и CI
- Solution: **`9-tests/XDiary.Tests.sln`** (7 проектов).
- CI (**DO-009**): `dotnet test 9-tests/XDiary.Tests.sln` на ubuntu-latest (Linux).
- Локально (Windows, это окружение) сборка 6-msa-зависимых тест-проектов падает **MSB4276** (битые workload-локаторы .NET SDK 8.0.419) и **NU1900** (нет сети) — это среда, не код. E2E-проект (без EF Core) собирается. Авторитетный прогон — CI.
- E2E/нагрузка дополнительно требуют `XDIARY_E2E_BASE_URL` (стенд) и `XDIARY_E2E_TOKEN`/`XDIARY_E2E_PIN`.
