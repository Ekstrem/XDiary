# BC-Journal — микросервис `journal`

Журнал интимной активности (Core Domain, роль Execution). Один ограниченный контекст
`Journal` и один агрегат записи **Record**.

Реализован по скиллу **edm-net** (EDM .NET / `ddd-microservice-csharp`) на
**DigiTFactory.Libraries** (SeedWorks + AbstractAggregate) по архитектуре **CEQRS**,
профиль **Single** (ADR-002): 1 процесс (Cmd + Evt + Qry), EventBus InMemory,
Command Store = Read Store = PostgreSQL (та же БД).

## Агрегат Record

| Поле | Тип |
|---|---|
| startedAt | string `YYYY-MM-DDTHH:mm` |
| durationMin | int |
| partner, place | string |
| positions, tags, costumes, toys, fantasies, contraceptives | string[] |
| contraceptiveCount | int |
| orgasmsMe / orgasmsPartner | int |
| orgasmMeType / orgasmPartnerType | string[] |
| orgasmMeRating / orgasmPartnerRating | int 0–5 |
| garmin, garminId | bool / string |

## Command / Query API (`/api/v1/journal`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/journal` | CreateRecord |
| PUT | `/api/v1/journal/{id}` | UpdateRecord |
| DELETE | `/api/v1/journal/{id}` | DeleteRecord |
| POST | `/api/v1/journal/import` | ImportRecord (upsert) |
| GET | `/api/v1/journal/{id}` | GetRecordById |
| GET | `/api/v1/journal` | ListRecords (startedAt DESC) |
| POST | `/api/v1/journal/admin/rebuild-projections` | Rebuild проекций |

## Структура

```
src/
├── Journal.Domain/          # IJournal, IRecordAggregate, MetadataRecordAggregate, метаданные
├── Journal.DomainServices/  # AggregateProvider, Notifier, BusAdapter, DI
├── Journal.InternalContracts/ # JournalReadModel, IQueryRepository
├── Journal.Application/     # MediatR Commands/Queries
├── Journal.Storage/         # CommandDbContext (event store), ReadDbContext, проекции
├── Journal.Api/             # Program.cs, контроллеры
└── Journal.Domain.Tests/    # доменные тесты (in-memory метаданные)
```

## Сборка и тесты

```bash
cd src
dotnet restore Journal.sln
dotnet build Journal.sln --configuration Release
dotnet test Journal.Domain.Tests/Journal.Domain.Tests.csproj --configuration Release
```

## Docker

```bash
docker build -t journal:single -f Dockerfile .
docker run --rm -p 8080:8080 journal:single
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-journal:single` (и `:latest`).

## Связь с артефактами DDD

- Канвасы: `03-ddd/BC-Journal/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md` (1 BC = 1 сервис), `adr-002.md` (Single)
- Бэклог: `5-tech-backlog/back-end/BE-002.md`
