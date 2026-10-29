# Command API: Дневные метрики

> Внутренний API. Источник — локальный прокси/CSV.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| SyncStress | garminSync() → `/local/stress` → importWeekly(items, "stress") | stress | Stored |
| SyncSleep | garminSync() → `/local/sleep` → DB.putSleepBatch | sleep | Stored |
| SyncHrv | garminSync() → `/local/hrv` → importWeekly(items, "hrv") | hrv | Stored |
| ImportCsv | handleImportAnyFile() | CSV (stress/sleep/resting) | Stored |

## Request DTO
### StressBatch (из `/local/stress`)
```json
{ "stress": { "YYYY-MM-DD": int } }
```

## Цепочка корреляции имён
```
UI/автосинк → garminSync() → garminFetch("/local/*") → importWeekly / DB.putSleepBatch → StressImported/…
CSV → handleImportAnyFile() → parse → DB.put* → …Imported
```
