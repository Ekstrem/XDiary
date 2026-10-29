# 5. Materialized Views

Канонический артефакт: [`03-ddd/BC-Journal/05-materialized-views.md`](../../../03-ddd/BC-Journal/05-materialized-views.md)

| Store | Таблица | Key | Индекс |
|---|---|---|---|
| Read Store (PostgreSQL) | `records` | `id` | `startedAt` |

## Проекционные правила

| Событие | Действие |
|---|---|
| RecordCreated | UPSERT (insert) |
| RecordUpdated | UPSERT |
| RecordDeleted | DELETE |
| RecordImported | UPSERT (по id) |

Проекция выполняется в `JournalProjectionHandler` (подписан на InMemory EventBus),
события параллельно сохраняются в Command Store через `JournalCommandDbProjector`.
