# BC-Portability — микросервис `portability`

Переносимость данных (Supporting Domain, роль Execution). Один ограниченный контекст
`Portability` и один агрегат **CsvDocument** (Published Language для CSV-обмена).

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002).

## CSV (Published Language)

Заголовки фиксированы (`CSV_HEADERS`), списки полей — через `;` (позы, теги, игрушки…).

```
id,started_at,duration_min,partner,place,positions,tags,costumes,toys,fantasies,contraceptives,contraceptive_count,orgasms_me,orgasms_partner,orgasm_me_type,orgasm_me_rating,orgasm_partner_type,orgasm_partner_rating,garmin
```

## Command / Query API (`/api/v1/portability`)

| Метод | Эндпоинт | Назначение |
|---|---|---|
| GET | `/api/v1/portability/headers` | CSV_HEADERS (контракт) |
| POST | `/api/v1/portability/export` | Записи → CSV |
| POST | `/api/v1/portability/import` | CSV → записи (распознавание по заголовку) |
| POST | `/api/v1/portability/document` | UpsertDocument (upsert по sourceId) |
| GET | `/api/v1/portability/document?sourceId=…` | GetDocument |

Импорт идемпотентный: merge по `id` (не дублирует записи) — следующий этап (обмен с journal через query-API).

## Сборка и тесты

```bash
cd src
dotnet restore Portability.sln
dotnet build Portability.sln --configuration Release
dotnet test Portability.Domain.Tests/Portability.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-portability:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-Portability/01..05-*.md`
- ADR: `04-architecture/ADL/adr-011.md`
- Бэклог: `5-tech-backlog/back-end/BE-011.md`
