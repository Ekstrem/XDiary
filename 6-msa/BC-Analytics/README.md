# BC-Analytics — микросервис `analytics`

Аналитика (Supporting Domain, роль Read-only). Один ограниченный контекст `Analytics`.
**Нет агрегата записи** — только вычисляемые проекции над Журналом и Соматикой.

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks), архитектура **CEQRS**,
профиль **Single** (ADR-002). Read-only: не пишет, не хранит — вычисляет на лету.

## Проекции (read models)

| Проекция | Описание | Статус |
|---|---|---|
| SummaryCards | сессии, суммарное время, средняя длительность, оргазмы, средняя оценка, период 30/90/all | ✅ |
| CorrelationResult | Pearson (длительность↔пульс покоя, оргазмы↔пульс покоя), n ≥ 3, пояснение | ✅ |
| PhysioProfile / TopLists / Distribution / Thresholds | следующие инкременты (ST-05..ST-08) | ⬜ |

## Query API (`/api/v1/analytics`)

| Метод | Эндпоинт | Запрос |
|---|---|---|
| POST | `/api/v1/analytics/summary` | GetSummary (period + records) |
| POST | `/api/v1/analytics/correlations` | GetCorrelations (records + metrics) |

> Полное межсервисное чтение (через query-API journal/daily-metrics по HTTP) — следующий этап:
> сервис получает `records`/`metrics` на вход и вычисляет проекции.

## Сборка и тесты

```bash
cd src
dotnet restore Analytics.sln
dotnet build Analytics.sln --configuration Release
dotnet test Analytics.Domain.Tests/Analytics.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-analytics:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-Analytics/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-002.md`
- Бэклог: `5-tech-backlog/back-end/BE-009.md`
