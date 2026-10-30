# Command API: Каталог и инвентарь (Catalog)

> Именованные команды → методы агрегата → события. Id/Version — `<guid, long>` фреймворка EDM (DF).

## Команды
| Команда | Метод агрегата | Событие | Входные VO |
|---|---|---|---|
| AddItem | `AddItem(kind, name)` | `ItemAdded` | kind, name |
| Rename | `Rename(name)` | `ItemRenamed` | name |
| UpdateDetails | `UpdateDetails(photo, notes, brand, fantasyPartner)` | `ItemDetailsUpdated` | Photo, Notes, Brand, FantasyPartner |
| Archive | `Archive()` | `ItemArchived` | Archived |
| Restore | `Restore()` | `ItemRestored` | Archived |
| Remove | `Remove()` | `ItemRemoved` | — |

## Request DTO (ItemDraft)
```json
{ "kind": "costume|toy|fantasy|contraception", "name": "string", "photo": "dataURL", "notes": "string", "brand": "string", "fantasyPartner": "string" }
```

## Цепочка корреляции имён
```
UI → AddItem(kind, name) → ItemAdded
UI → Rename(name) → ItemRenamed
UI → UpdateDetails(...) → ItemDetailsUpdated
UI → Archive() → ItemArchived → Restore() → ItemRestored
UI → Remove() → ItemRemoved
```
