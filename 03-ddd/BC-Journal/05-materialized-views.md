# Materialized Views: Журнал интимной активности (Journal)

## Проекция (ReadModel)
| Store | Тип | Обслуживает |
|---|---|---|
| `records` | ReadModel (keyPath `id`) | JournalView, RecordDetailView |
| `journal_recent` | MV (последние значения) | RecentDefaultsView (`recent`) |
| `journal_places` | MV (уникальные места) | подсказки мест (`places`) |

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

## MV: journal_recent (дефолты визарда)
| Поле | Тип | Источник |
|---|---|---|
| lastPartnerId | uuid\|null | `RecordCompleted` (первый партнёр последней записи) |
| lastPartnerName | string\|null | проекция из `Partner` |
| lastPlace | string\|null | `RecordCompleted` |

## MV: journal_places (уникальные места)
| Поле | Тип |
|---|---|
| place | string (key) |
| lastUsedAt | string |

## Проекционные правила (событие → действие)
| Событие | `records` | `journal_recent` | `journal_places` |
|---|---|---|---|
| `RecordCompleted` | INSERT / upsert | upsert (latest по `startedAt`) | upsert `place` + `lastUsedAt` |
| `RecordDeleted` | DELETE | пересчёт latest | очистка (если `place` больше не используется) |

## Индексы
- `records.startedAt` — сортировка журнала (по убыванию).
- `records.partners` — фильтр по партнёру.
- `journal_places.lastUsedAt` — сортировка подсказок по свежести.
