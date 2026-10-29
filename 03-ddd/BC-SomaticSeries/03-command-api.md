# Command API: Соматика — минутные точки

> Внутренний API. Источник — локальный прокси/CSV.

## Команды
| Команда | Функция (код) | Входные VO | Состояние до |
|---|---|---|---|
| SyncSomatic | garminSync() → `/local/somatic` → DB.putSomaticBatch | точки | Stored |
| ImportCsv | handleImportAnyFile() | CSV (somatic) | Stored |

## Request DTO
### SomaticBatch (из `/local/somatic`)
```json
{ "somatic": [["YYYY-MM-DDTHH:mm", hr, body_battery]] }
```

## Цепочка корреляции имён
```
UI/автосинк → garminSync() → garminFetch("/local/somatic") → DB.putSomaticBatch → SomaticImported
CSV → handleImportAnyFile() → parse → DB.putSomaticBatch → SomaticImported
```
