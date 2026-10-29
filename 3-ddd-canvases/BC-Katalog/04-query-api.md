# Query API: Каталог и инвентарь

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Список по виду | renderCatalog() | CatalogView (фильтр по kind) | `catalog` |
| Форма редактирования | openCatalogEdit(id, kind) | ItemDraft | `catalog` |
| Средства контрацепции | stepContraception() | список (неархивные) | `catalog` (kind=contraception) |
| Фантазии (для отметки) | stepMarks() | список | `catalog` (kind=fantasy) |

## Response DTO
- CatalogView: строки (аватар, имя, бренд/партнёр/заметки, пометка «(архив)», значок ✓ реализованной фантазии).

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI пользователя | все | управление каталогом |
| Журнал | stepMarks, stepContraception | выбор элементов в запись |
