# Query API: Переносимость данных (Portability)

## Запросы (чтение)
| Запрос | Эндпоинт | Response |
|---|---|---|
| Заголовки CSV | `GET /api/v1/portability/headers` | список колонок |
| Распознавание типа | (по заголовку) | тип CSV |

## Response DTO
- Типы CSV: records / somatic / sleep / stress / resting_hr.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| UI | экспорт/импорт | перенос и резерв |
