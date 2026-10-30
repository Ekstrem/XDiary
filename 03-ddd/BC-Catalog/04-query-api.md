# Query API: Каталог и инвентарь (Catalog)

> Read-сторона (CEQRS): только из ReadModel.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Список по виду | `GET /api/v1/catalog?kind=toy` | CatalogView | ReadModel `catalog` |
| Элемент | `GET /api/v1/catalog/{id}` | ItemView | ReadModel |
| Средства контрацепции | `GET /api/v1/catalog?kind=contraception` | список | ReadModel |

## Response DTO
- CatalogView: строки (имя, бренд/партнёр, «(архив)», ✓ реализованной фантазии).
- ItemView: kind, name, photo, notes, brand, archived, partner.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | все | управление каталогом |
| Журнал | имена элементов | выбор в запись |
