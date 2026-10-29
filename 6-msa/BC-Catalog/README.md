# BC-Catalog — микросервис `catalog`

Каталог и инвентарь (Supporting Domain, роль Execution). Один ограниченный контекст
`Catalog` и один агрегат **CatalogItem**.

Реализован по скиллу **edm-net** на **DigiTFactory.Libraries** (SeedWorks + AbstractAggregate)
по архитектуре **CEQRS**, профиль **Single** (ADR-002).

## Агрегат CatalogItem

| Поле | Тип |
|---|---|
| kind | string (`costume` / `toy` / `fantasy` / `contraception`) |
| name | string |
| photo | string (dataURL) |
| notes | string |
| brand | string (только toy) |
| archived | bool (только toy) |
| partner | string (только fantasy) |

## Command / Query API (`/api/v1/catalog`)

| Метод | Эндпоинт | Команда/запрос |
|---|---|---|
| POST | `/api/v1/catalog` | CreateItem |
| PUT | `/api/v1/catalog/{id}` | UpdateItem (в т.ч. архив/возврат) |
| DELETE | `/api/v1/catalog/{id}` | DeleteItem |
| GET | `/api/v1/catalog/{id}` | GetCatalogItemById |
| GET | `/api/v1/catalog` | ListCatalogItems |
| POST | `/api/v1/catalog/admin/rebuild-projections` | Rebuild проекций |

## Сборка и тесты

```bash
cd src
dotnet restore Catalog.sln
dotnet build Catalog.sln --configuration Release
dotnet test Catalog.Domain.Tests/Catalog.Domain.Tests.csproj --configuration Release
```

Образ публикуется в GHCR: `ghcr.io/ekstrem/xdiary-catalog:single` (и `:latest`).

## Связь с артефактами

- Канвасы: `03-ddd/BC-Catalog/01..05-*.md`
- ADR: `04-architecture/ADL/adr-001.md`, `adr-002.md`
- Бэклог: `5-tech-backlog/back-end/BE-004.md`
