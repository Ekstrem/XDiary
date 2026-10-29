# BC-GarminGateway — микросервис `garmin-gateway`

Шлюз-ACL к Garmin (Supporting Domain, роль Gateway). Один ограниченный контекст
`GarminGateway` и один агрегат **GarminSync** (состояние интеграции).

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002). Агрегат-синглтон на источник.

## Агрегат GarminSync

| Поле | Тип |
|---|---|
| sourceId | string (`garmin`) |
| apiKey / oauthToken / tokenExpiry | string (Credentials) |
| metrics / windowDays | string[] / int (SyncConfig) |
| lastSync | string (LastSync) |

## Command / Query API (`/api/v1/garmin-gateway`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/garmin-gateway` | UpsertSyncState (upsert по sourceId) |
| GET | `/api/v1/garmin-gateway/state?sourceId=garmin` | GetSyncState |
| POST | `/api/v1/garmin-gateway/admin/rebuild-projections` | Rebuild проекций |

> Фактический вызов Personal API Garmin (OAuth, пошаговый синк) — следующий этап:
> сервис-ACL (HttpClient) вызывает Garmin, публикует нормализованную соматику/занятия
> наружу (somatic-series / daily-metrics / journal), а здесь хранится состояние синка.

## Сборка и тесты

```bash
cd src
dotnet restore GarminGateway.sln
dotnet build GarminGateway.sln --configuration Release
dotnet test GarminGateway.Domain.Tests/GarminGateway.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-garmin-gateway:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-GarminGateway/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-012.md`
- Бэклог: `5-tech-backlog/back-end/BE-007.md`
