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

**Внутренний маршрут (добавлено 2026-11-10, @AR):** `GET /internal/daily-metrics/series` — `DailyMetrics.InternalSeries`,
`auth: jwt-service`, `internal: true` в каноне. Он **не публикуется** в `openapi/*.json` и проверяется отдельно контуром `(ii-c)`,
поэтому в таблице выше его нет. Записан здесь, чтобы «нет в таблице» не читалось как «нет маршрута».
