# Query API: Журнал интимной активности (Journal)

> Read-сторона (CEQRS): читает только из ReadModel, никогда из Domain/Event Store.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Журнал (список) | `GET /api/v1/journal?filter=...` | JournalView | ReadModel `records` |
| Деталь записи | `GET /api/v1/journal/{id}` | RecordDetailView | ReadModel + соматика |
| Черновик | `GET /api/v1/journal/draft/{id}` | RecordDraft | Draft (не проекция) |

## Response DTO
### JournalView
- Список карточек: `partners[]`, `place`, `durationMin`, суммарные оргазмы, chips (позы/аксессуары/контрацепция/теги), значок ⌚.

### RecordDetailView
- Мета: `startedAt`, `partners[]`, `place`, `durationMin`, `orgasms[]` (по участникам).
- Соматика: пульс (ср/макс), Body Battery Δ, стресс, сон, HRV.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | все | просмотр журнала и деталей |
| Аналитика | JournalView | источник записей для корреляций |
