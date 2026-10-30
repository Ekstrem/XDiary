# Materialized Views: Каталог и инвентарь (Catalog)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `catalog` | ReadModel (keyPath `id`, index `kind`) | CatalogView |

## Store: catalog
| Поле | Тип | Источник |
|---|---|---|
| id | uuid PK | фреймворк DF (`<guid, long>`) |
| kind | string | сущность |
| name | string | сущность |
| photo | string (dataURL) | VO |
| notes | string | VO |
| brand | string | VO (toy) |
| archived | bool | VO (toy) |
| partner | string | VO (fantasy, 0..1) |
| createdAt / updatedAt | string | системное |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `ItemAdded` | INSERT |
| `ItemRenamed` | PUT (name) |
| `ItemDetailsUpdated` | PUT (photo/notes/brand/partner) |
| `ItemArchived` | PUT (archived=true) |
| `ItemRestored` | PUT (archived=false) |
| `ItemRemoved` | DELETE |

## Производные проекции (на лету)
- Сортировка: активные впереди, архивные внизу.
- «Реализованная фантазия» — пересечение fantasy-имён с `records.fantasies`.
