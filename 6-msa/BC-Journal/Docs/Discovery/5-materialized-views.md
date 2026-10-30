# Materialized Views: Журнал интимной активности (Journal)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `records` | ReadModel (keyPath `id`) | JournalView, RecordDetailView |

## Store: records
| Поле | Тип | Источник |
|---|---|---|
| id | uuid PK | фреймворк DF (`<guid, long>`) |
| startedAt | string | сущность |
| partners | uuid[] | сущность (0..N) |
| place | string | VO |
| positions | string[] | VO |
| tags | string[] | VO |
| costumes | string[] | VO |
| toys | string[] | VO |
| fantasies | string[] | VO |
| contraceptives | string[] | VO |
| contraceptiveCount | int | VO |
| durationMin | int | VO |
| orgasms | json[] | VO `{participant, count, types[], rating}` |
| garmin | bool | VO |
| garminId | string | VO |
| createdAt / updatedAt | string | системное |

## Проекционные правила (событие → действие)
| Событие | Действие |
|---|---|
| `RecordCompleted` | INSERT / upsert |
| `RecordDeleted` | DELETE |

## Индексы
- `startedAt` — сортировка журнала по убыванию.
- `partners` — фильтр по партнёру.
