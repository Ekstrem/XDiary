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
```

## Статус по TE
- TE-001 (journal) — ✅ закрыта (проверено TL).
- TE-002 (partners+catalog) — ✅ закрыта.
- TE-003 (somatic+daily) — ✅ закрыта.
- TE-004 (gateways) — на проверке.
- TE-005 (analytics+privacy) — на проверке; **gap:** JWT-выдача в privacy не реализована (только verify на шлюзе).
- TE-006 (portability+gateway) — на проверке; **gap:** идемпотентность импорта по `id` не реализована.
- TE-007…TE-009 (E2E) — ждут FE-001 (сейчас «На проверке»).
- TE-010…TE-011 (нагрузка) — нужна развёрнутая среда (VPS).
- CI: **DO-009** — DevOps добавляет workflow для `9-tests`.
