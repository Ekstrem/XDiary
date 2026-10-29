# Query API: Соматика

## Запросы (чтение)
| Запрос | Функция (код) | Response | Источник |
|---|---|---|---|
| Срез вокруг акта | renderSomatic() / getSomaticRange | SomaticSlice | `somatic` (t в окне) |
| Дневные тренды | renderTrends() / getWeekly | DailySeries | `weekly`, `sleep` |
| График | drawChart() | Canvas | срез/тренды |

## Response DTO
- SomaticSlice: точки (t, hr, body_battery) в окне ±15/30/60 мин.
- DailySeries: стресс, пульс покоя, сон (оценка), HRV по датам.

## Потребители
| Потребитель | Запросы | Назначение |
|---|---|---|
| Журнал | SomaticSlice | сводка соматики на детали записи |
| Аналитика | DailySeries | тренды и корреляции |
