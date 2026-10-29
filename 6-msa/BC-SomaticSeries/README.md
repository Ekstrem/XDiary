# BC-SomaticSeries — микросервис `somatic-series`

Соматика — минутные точки (Supporting Domain, роль Execution). Один ограниченный контекст
`SomaticSeries` и один агрегат **SomaticPoint**.

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002). Append-only: импорт точек + срез по времени.

## Агрегат SomaticPoint

| Поле | Тип |
|---|---|
| t | string `YYYY-MM-DDTHH:mm` (минутная гранулярность) |
| hr | int |
| body_battery | int |

## Command / Query API (`/api/v1/somatic-series`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/somatic-series` | ImportPoint (upsert по t) |
| GET | `/api/v1/somatic-series/slice?startedAt=…&windowMin=15` | GetSomaticSlice (±N минут) |
| POST | `/api/v1/somatic-series/admin/rebuild-projections` | Rebuild проекций |

## Сборка и тесты

```bash
cd src
dotnet restore SomaticSeries.sln
dotnet build SomaticSeries.sln --configuration Release
dotnet test SomaticSeries.Domain.Tests/SomaticSeries.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-somatic-series:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-SomaticSeries/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-002.md`
- Бэклог: `5-tech-backlog/back-end/BE-005.md`
