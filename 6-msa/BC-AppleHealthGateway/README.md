# BC-AppleHealthGateway — микросервис `apple-health-gateway`

Шлюз-ACL к Apple Health (Supporting Domain, роль Gateway). Один ограниченный контекст
`AppleHealthGateway` и один агрегат **AppleImport** (состояние импорта).

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002). Агрегат-синглтон на источник.

## Агрегат AppleImport

| Поле | Тип |
|---|---|
| sourceId | string (`apple-health`) |
| metrics | string[] (ImportConfig) |
| lastImport | string (LastImport) |
| processedFiles | string[] (ProcessedFiles — идемпотентность по имени файла) |

## Command / Query API (`/api/v1/apple-health-gateway`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/apple-health-gateway` | UpsertImportState (upsert по sourceId) |
| GET | `/api/v1/apple-health-gateway/state?sourceId=apple-health` | GetImportState |
| POST | `/api/v1/apple-health-gateway/admin/rebuild-projections` | Rebuild проекций |

> Фактический парсинг `export.xml`/CSV и выдача нормализованной соматики
> в somatic-series/daily-metrics — следующий этап (сервис-ACL); здесь хранится состояние импорта.

## Сборка и тесты

```bash
cd src
dotnet restore AppleHealthGateway.sln
dotnet build AppleHealthGateway.sln --configuration Release
dotnet test AppleHealthGateway.Domain.Tests/AppleHealthGateway.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-apple-health-gateway:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-AppleHealthGateway/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-012.md`
- Бэклог: `5-tech-backlog/back-end/BE-008.md`
