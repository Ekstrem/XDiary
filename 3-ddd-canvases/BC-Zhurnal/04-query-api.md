# Query API: Журнал интимной активности

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Журнал (список) | renderJournal() | JournalView | `records` (index startedAt, DESC) |
| Деталь записи | openDetail(id) → renderDetail() | RecordDetailView | `records` + `somatic` + `weekly` + `sleep` |
| Черновик записи | openWizard(id) | RecordDraft | `records` |

## Response DTO

### JournalView
- Список карточек: partner, place, durationMin, суммарные оргазмы, chips (позы/аксессуары/контрацепция/теги), значок ⌚ (garmin).

### RecordDetailView
- Мета: дата, партнёр, место, длительность, оргазмы (тип+оценка).
- Сводка соматики: пульс (ср/макс), Body Battery Δ, стресс за день, сон, HRV.
- График: пульс / Body Battery / стресс вокруг startedAt (±15/30/60 мин).

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI пользователя | все | просмотр журнала и деталей |
| Аналитика | renderJournal → записи | источник данных для корреляций |
