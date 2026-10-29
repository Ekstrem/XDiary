# BC-DailyMetrics — микросервис `daily-metrics`

Дневные метрики (Supporting Domain, роль Execution). Один ограниченный контекст
`DailyMetrics` и один агрегат **DailyMetrics**.

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002). Append-only: импорт сводок + тренды по датам.

## Агрегат DailyMetrics

| Поле | Тип |
|---|---|
| date | string `YYYY-MM-DD` |
| stress | int |
| resting_hr | int |
| hrv | int |
| sleepScore / sleepDurationMin / deepMin / lightMin / remMin / awakeMin | int |

## Command / Query API (`/api/v1/daily-metrics`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/daily-metrics` | ImportDaily (upsert по date) |
| GET | `/api/v1/daily-metrics/series?fromDate=…&toDate=…` | GetDailySeries |
| POST | `/api/v1/daily-metrics/admin/rebuild-projections` | Rebuild проекций |

## Сборка и тесты

```bash
cd src
dotnet restore DailyMetrics.sln
dotnet build DailyMetrics.sln --configuration Release
dotnet test DailyMetrics.Domain.Tests/DailyMetrics.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-daily-metrics:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-DailyMetrics/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-002.md`
- Бэклог: `5-tech-backlog/back-end/BE-006.md`
