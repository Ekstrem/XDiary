# Query API: Журнал интимной активности (Journal)

> Read-сторона (CEQRS): читает только из ReadModel, никогда из Domain/Event Store.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Журнал (список) | `GET /api/v1/journal?limit=&offset=&filter=...` | JournalView | ReadModel `records` |
| Деталь записи | `GET /api/v1/journal/{id}` | RecordDetailView | ReadModel + соматика |
| Черновик | `GET /api/v1/journal/draft/{id}` | RecordDraft | Draft (не проекция) |
| Последние (визард) | `GET /api/v1/journal/recent` | RecentDefaultsView | MV `journal_recent` |
| Места (подсказки) | `GET /api/v1/journal/places` | `string[]` | MV `journal_places` |
| Реализованные фантазии (бейдж каталога) | `GET /api/v1/journal/fantasies` | `string[]` | MV `journal_fantasies` |

## Response DTO
### JournalView
- Список карточек: `partners[]`, `place`, `durationMin`, суммарные оргазмы, chips (позы/аксессуары/контрацепция/теги), значок ⌚.

### RecordDetailView
- Мета: `startedAt`, `partners[]`, `place`, `durationMin`, `orgasms[]` (по участникам).
- Соматика: пульс (ср/макс), Body Battery Δ, стресс, сон, HRV.

### RecentDefaultsView
- `lastPartner`: `{ id, name } | null` — первый партнёр последней записи (`null` если соло/нет).
- `lastPlace`: `string | null` — место последней записи.
- `lastPositions`: `string[]` — позы последней записи.
- `lastTags`: `string[]` — теги последней записи.
- `lastContraceptives`: `string[]` — контрацепция последней записи.
- `lastFantasies`: `string[]` — реализованные фантазии последней записи.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | все | просмотр журнала и деталей |
| Аналитика | JournalView | источник записей для корреляций |
