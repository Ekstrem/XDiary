# Query API: Дневные метрики (DailyMetrics)

> Read-сторона (CEQRS): только из ReadModel.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Дневные тренды | `GET /api/v1/daily-metrics?from=…&to=…` | DailySeries | ReadModel `weekly`, `sleep` |

## Response DTO
- DailySeries: стресс, пульс покоя, сон (оценка), HRV по датам.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Аналитика | DailySeries | тренды и корреляции |
