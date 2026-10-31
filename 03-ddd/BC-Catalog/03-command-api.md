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

## Request DTO (ItemDraft)
```json
{ "kind": "costume|toy|fantasy|contraception", "name": "string", "photo": "dataURL",
  "notes": "string", "brand": "string", "fantasyPartner": "string" }
```
