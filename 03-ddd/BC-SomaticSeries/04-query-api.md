# Query API: Соматика — минутные точки (SomaticSeries)

> Read-сторона (CEQRS): только из ReadModel.

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Срез вокруг акта | `GET /api/v1/somatic-series?from=…&to=…` | SomaticSlice | ReadModel `somatic` |

## Response DTO
- SomaticSlice: точки (t, hr, body_battery) в окне ±15/30/60 мин.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Журнал | SomaticSlice | сводка соматики на детали записи |
| Аналитика | SomaticSlice | физиопрофиль сессии |
