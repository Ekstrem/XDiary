# Query API: Шлюз Garmin

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Список занятий | renderGarminPicker() | Activities | `/activities` |

## Response DTO
- Activities: список занятий (start, end, name, hr) для выбора кандидата в акт.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Журнал (автоперенос) | Activities | «Из Garmin» — выбор занятия |
