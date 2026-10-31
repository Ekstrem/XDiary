# Command API: Каталог и инвентарь (Catalog)

> Команды → методы агрегата → события. Конвенция EDM/DF — **только POST**. ⚠️ = техдолг (AR-003).

## Команды
| Команда | Эндпоинт (факт 6-msa) | Метод агрегата | Событие |
|---|---|---|---|
| AddItem | `POST /api/v1/catalog` | `AddItem(kind, name)` | `ItemAdded` |
| Rename | `PUT /api/v1/catalog/{id}/name` ⚠️ | `Rename(name)` | `ItemRenamed` |
| UpdateDetails | `PUT /api/v1/catalog/{id}/details` ⚠️ | `UpdateDetails(...)` | `ItemDetailsUpdated` |
| Archive | `POST /api/v1/catalog/{id}/archive` | `Archive()` | `ItemArchived` |
| Restore | `POST /api/v1/catalog/{id}/restore` | `Restore()` | `ItemRestored` |
| Remove | `DELETE /api/v1/catalog/{id}` ⚠️ | `Remove()` | `ItemRemoved` |

## Request DTO (ItemDraft)
```json
{ "kind": "costume|toy|fantasy|contraception", "name": "string", "photo": "dataURL",
  "notes": "string", "brand": "string", "fantasyPartner": "string" }
```

> ⚠️ **Техдолг (AR-003):** `Rename`/`UpdateDetails` (PUT) и `Remove` (DELETE) должны быть `POST .../{action}`.
