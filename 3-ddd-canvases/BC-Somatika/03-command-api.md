# Command API: Соматика

> Внутренний API. Источник данных — локальный прокси/CSV (не напрямую Garmin).

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| SyncStress | garminSync() → `/local/stress` → importWeekly(items, "stress") | stress по дням | Stored |
| SyncSomatic | garminSync() → `/local/somatic` → DB.putSomaticBatch | точки | Stored |
| SyncSleep | garminSync() → `/local/sleep` → DB.putSleepBatch | sleep | Stored |
| SyncHrv | garminSync() → `/local/hrv` → importWeekly(items, "hrv") | hrv | Stored |
| ImportCsv | handleImportAnyFile() | CSV (somatic/sleep/stress/resting) | Stored |

## Входящие «события» извне
| Событие | Команда | Источник |
|---|---|---|
| автосинк при входе | garminSync(true) | Приватность (после разблокировки) |

## Request DTO
### SomaticBatch (из `/local/somatic`)
```json
{ "somatic": [["YYYY-MM-DDTHH:mm", hr, body_battery]], "resting_hr": {"date": int} }
```

## Цепочка корреляции имён
```
UI/автосинк → garminSync() → garminFetch("/local/*") → DB.putSomaticBatch / importWeekly → SomaticImported/…
CSV → handleImportAnyFile() → parse → DB.put* → …Imported
```
