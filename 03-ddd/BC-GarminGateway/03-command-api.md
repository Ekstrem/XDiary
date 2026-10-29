# Command API: Шлюз Garmin

> Внутренний API — ACL к Garmin (Personal API через прокси/CSV).

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| SyncSomatics | garminSync() → `/local/*` | SyncConfig | Configured |
| ImportActivities | openGarminPicker() → `/activities` | — | Configured |

## Request DTO
### SomaticBatch (из `/local/somatic`)
```json
{ "somatic": [["YYYY-MM-DDTHH:mm", hr, body_battery]] }
```

## Цепочка корреляции имён
```
UI/автосинк → garminSync() → garminFetch("/local/*") → нормализованные DTO → SomaticSeries/DailyMetrics
UI «Из Garmin» → openGarminPicker() → `/activities` → занятия → Журнал
```
