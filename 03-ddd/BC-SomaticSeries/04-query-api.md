# Query API: Соматика — минутные точки

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Срез вокруг акта | renderSomatic() / getSomaticRange | SomaticSlice | `somatic` (t в окне) |
| График | drawChart() | Canvas | срез |

## Response DTO
- SomaticSlice: точки (t, hr, body_battery) в окне ±15/30/60 мин.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Журнал | SomaticSlice | сводка соматики на детали записи |
