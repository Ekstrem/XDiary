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
| lastPositions | string[] | `RecordCompleted` |
| lastTags | string[] | `RecordCompleted` |
| lastContraceptives | string[] | `RecordCompleted` |
| lastFantasies | string[] | `RecordCompleted` |

## MV: journal_places (уникальные места)
| Поле | Тип |
|---|---|
| place | string (key) |
| lastUsedAt | string |

## MV: journal_fantasies (реализованные фантазии)
| Поле | Тип |
|---|---|
| fantasy | string (key) |
| lastUsedAt | string |

## Проекционные правила (событие → действие)
| Событие | `records` | `journal_recent` | `journal_places` | `journal_fantasies` |
|---|---|---|---|---|
| `RecordCompleted` | INSERT / upsert | upsert (latest по `startedAt`) | upsert `place` + `lastUsedAt` | upsert `fantasy` + `lastUsedAt` |
| `RecordDeleted` | DELETE | пересчёт latest | очистка (если не используется) | очистка (если не используется) |

## Индексы
- `records.startedAt` — сортировка журнала (по убыванию).
- `records.partners` — фильтр по партнёру.
- `journal_places.lastUsedAt` — сортировка подсказок по свежести.

## Физические таблицы: что реально создаётся (сверено 2026-11-10, @AR)

| Логический store (выше) | Физическая таблица | Где создаётся |
|---|---|---|
| `records` | **`records`** | `Journal.Storage/Migrations/20261030112445_InitialCreate.cs` |
| — (справочник для резолва имён) | **`partners_directory`** | `Migrations/ReadStore/20261105170525_AddReadStores.cs` |
| — | **`journal_read`** | там же |
| — | **`journal_defaults`** | там же |

**Расхождение, которое надо назвать:** `journal_recent`, `journal_places`, `journal_fantasies` из таблиц выше **не создаются как таблицы** — в миграциях таких имён нет.
Значит либо это **логические** представления над `journal_read`/`journal_defaults` (тогда это надо записать явно), либо они **не реализованы** (тогда разделы выше описывают замысел, а не состояние).
Проверка: `git grep -n 'CREATE TABLE' -- 6-msa/BC-Journal` даёт ровно четыре имени выше. Вопрос — @BE: какой из двух вариантов верен.

## Эталон сверки «поля read-модели ⊆ колонки DDL» (2026-11-10, @AR; вход для `DO-109`)

Замер по `origin`: поля считались по объявлению **своего** класса (не вложенных), колонки — по `table.Column`
внутри `CreateTable` в `Migrations/ReadStore/*` (файлы `*.Designer.cs`/`*Snapshot.cs` исключены, там дубли).

| Таблица | Полей модели | Колонок DDL | Расхождений |
|---|---|---|---|
| `journal_read` | **20** | **20** | 0 |
| `journal_defaults` | **9** | **9** | 0 |
| `partners_directory` | **4** (`Id`, `Name`, `ClientId`, `DeletedAt`) | **4** (`PartnerId`, `Name`, `ClientId`, `DeletedAt`) | **0** — `Id` ↔ `PartnerId` через `HasColumnName` (`PartnersDirectoryReadModel.cs:11-13`), а `PartnerId => Id` (`:14`) — вычисляемое свойство, не колонка |

**Итого эталон:** моделей с DDL — **3** (в BC-Journal), полей — **33**, расхождений — **0**.
**Требование к гейту:** учитывать `HasColumnName` и **не считать** computed-свойства (`=>`) колонками, иначе он даст ложный красный на паре `Id`/`PartnerId`.

