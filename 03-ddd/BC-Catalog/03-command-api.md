# Command API: Каталог и инвентарь (Catalog)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**.

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| AddItem | `POST /api/v1/catalog` | `AddItem(kind, name)` | `ItemAdded` |
| Rename | `POST /api/v1/catalog/{id}/name` | `Rename(name)` | `ItemRenamed` |
| UpdateDetails | `POST /api/v1/catalog/{id}/details` | `UpdateDetails(...)` | `ItemDetailsUpdated` |
| Archive | `POST /api/v1/catalog/{id}/archive` | `Archive()` | `ItemArchived` |
| Restore | `POST /api/v1/catalog/{id}/restore` | `Restore()` | `ItemRestored` |
| Remove | `POST /api/v1/catalog/{id}/remove` | `Remove()` | `ItemRemoved` |

## Тела запросов (переписано 2026-11-10, @AR)

**Здесь был выдуманный DTO `ItemDraft`**, смешивавший поля двух разных команд. Типа с таким именем в коде **нет**.
Реальные тела (источник — `CatalogCommands.cs:6-9`, `CatalogController.cs:34,50`; канон совпадает):

| Команда | Тело (без `clientId` — из `sub`, без `id` — из маршрута) | Адрес |
|---|---|---|
| `AddItem` | `kind` · `name` | `AddCatalogItemCommand`, `CatalogCommands.cs:6` |
| `Rename` | `name` | `RenameCatalogItemCommand`, `:7` |
| `UpdateDetails` | `photo` · `notes` · `brand` · `fantasyPartner` | `UpdateCatalogItemDetailsCommand`, `:8` |
| `Archive` / `Restore` / `Remove` | **тела нет** — только `id` в маршруте | `CatalogController.cs:53-69` |

`kind` — одно из `costume | toy | fantasy | contraception` (см. §2 холста агрегата).
