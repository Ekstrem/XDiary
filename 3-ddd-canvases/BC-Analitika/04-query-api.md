# Query API: Аналитика и статистика

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Тренды | renderTrends() | TrendsView | `weekly` + `sleep` |
| Корреляции | renderCorrelations() | CorrelationsView | `records` + `weekly` + `sleep` |

## Response DTO
- TrendsView: график метрики (стресс/пульс покоя/сон/HRV).
- CorrelationsView: таблица (дата, мин, орг, пульс, макс, BB Δ, сон, стресс) + коэффициенты.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI пользователя | все | экран «Тренды» |
