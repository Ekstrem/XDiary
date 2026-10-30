# Query API: Шлюз Garmin (GarminGateway)

> Read-сторона (CEQRS).

## Запросы (чтение)
| Запрос | Эндпоинт | Response | Источник |
|---|---|---|---|
| Список занятий | `GET /api/v1/garmin/activities` | Activities | Garmin ACL |

## Response DTO
- Activities: список занятий (start, end, name, hr) для выбора кандидата в акт.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Журнал (автоперенос) | Activities | «Из Garmin» — выбор занятия |
