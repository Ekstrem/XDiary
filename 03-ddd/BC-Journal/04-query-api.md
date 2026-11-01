# Query API: Журнал интимной активности (Journal)

> Read-сторона (CEQRS): читает только из ReadModel, никогда из Domain/Event Store.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Журнал (список) | `GET /api/v1/journal?limit=&offset=&filter=...` | JournalView | ReadModel `records` |
| Деталь записи | `GET /api/v1/journal/{id}` | RecordDetailView | ReadModel + соматика |
| Черновик | `GET /api/v1/journal/draft/{id}` | RecordDraft | Draft (не проекция) |
| Последние (визард) | `GET /api/v1/journal/recent` | RecentDefaultsView | ReadModel `records` (latest по `startedAt`) |
| Места (подсказки) | `GET /api/v1/journal/places` | `string[]` | ReadModel `records` (DISTINCT `place`) |

## Response DTO
### JournalView
- Список карточек: `partners[]`, `place`, `durationMin`, суммарные оргазмы, chips (позы/аксессуары/контрацепция/теги), значок ⌚.

### RecordDetailView
- Мета: `startedAt`, `partners[]`, `place`, `durationMin`, `orgasms[]` (по участникам).
- Соматика: пульс (ср/макс), Body Battery Δ, стресс, сон, HRV.

### RecentDefaultsView
- `lastPartner`: `{ id, name } | null` — первый партнёр последней записи (`null` если соло/нет).
- `lastPlace`: `string | null` — место последней записи.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | все | просмотр журнала и деталей |
| Аналитика | JournalView | источник записей для корреляций |
