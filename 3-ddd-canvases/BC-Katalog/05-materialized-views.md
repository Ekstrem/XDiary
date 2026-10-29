# Materialized Views: Каталог и инвентарь

## Проекции (IndexedDB)
| Store | Тип | Обслуживает |
|---|---|---|
| `catalog` | Основная (keyPath `id`, поле `kind`) | CatalogView, stepMarks, stepContraception |

## Store: catalog
| Поле | Тип | Источник |
|---|---|---|
| id | string PK | uuid |
| kind | string | ItemDraft.kind |
| name | string | ItemDraft.name |
| photo | string (dataURL) | ItemDraft.photo |
| notes | string | ItemDraft.notes |
| brand | string | ItemDraft.brand (toy) |
| archived | bool | ItemDraft.archived (toy) |
| partner | string | ItemDraft.partner (fantasy) |
| createdAt / updatedAt | string | системное время |

## Проекционные правила
| Событие | Действие |
|---|---|
| ItemCreated | INSERT |
| ItemUpdated | PUT (upsert по id) |
| ItemDeleted | DELETE |

## Производные проекции (вычисляются на лету)
- Сортировка: активные впереди, архивные внизу (renderCatalog).
- «Реализованная фантазия» — пересечение fantasy-имён с записями `records.fantasies`.
- Контрацепция «недавние впереди» — сортировка по `records.contraceptives` (недавность).
