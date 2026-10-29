# Command API: Каталог и инвентарь

> Внутренний API (UI → сервис), без HTTP.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| CreateItem | saveCatalogEdit() | ItemDraft | (нет) |
| UpdateItem | saveCatalogEdit() | ItemDraft + editingCatalogId | Active |
| DeleteItem | deleteCatalogEdit() | editingCatalogId | Active/Archived |
| ArchiveItem | ce-archive → saveCatalogEdit() | archived | Active |

## Request DTO
### ItemDraft
```json
{
  "kind": "costume | toy | fantasy | contraception",
  "name": "string",
  "photo": "dataURL | null",
  "notes": "string",
  "brand": "string | null",
  "archived": false,
  "partner": "string | null"
}
```

## Цепочка корреляции имён
```
UI → openCatalogEdit(id, kind) → DB.getCatalogItem(id) → форма → saveCatalogEdit() → DB.putCatalogItem → ItemCreated/Updated
UI → deleteCatalogEdit() → DB.deleteCatalogItem(id) → ItemDeleted
```
