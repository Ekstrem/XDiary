# 4. Query API

Канонический артефакт: [`03-ddd/BC-Journal/04-query-api.md`](../../../03-ddd/BC-Journal/04-query-api.md)

| Запрос | HTTP | Response |
|---|---|---|
| Журнал (список) | `GET /api/v1/journal` | `JournalReadModel[]` (startedAt DESC) |
| Деталь записи | `GET /api/v1/journal/{id}` | `JournalReadModel` |

Query Side обращается **только к Read Store** (`IReadRepository<IJournal, JournalReadModel>`),
не к Domain/Event Store.
